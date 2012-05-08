using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace DaedalusTestApp.Comms
{
    /// <summary>
    /// Handles TCP communications using TPL Tasks.
    /// All methods are required to be thread safe.
    /// </summary>
    internal class TPLTCPQueuedComm : IDisposable
    {
        internal const int TCPResponseTimeoutMS = 10000;

        internal const uint packetBufferSize = 1024;

        // Defaults to using a ConcurrentQueue as the underlying data store
        internal BlockingCollection<NetPacket> inPackets = new BlockingCollection<NetPacket>();
        internal BlockingCollection<NetPacket> outPackets = new BlockingCollection<NetPacket>();
        internal CancellationTokenSource listenerTokenSource = new CancellationTokenSource();
        internal CancellationTokenSource clientTokenSource = new CancellationTokenSource();
        //public CancellationToken shutdownToken;
        internal List<PacketRegistryEntry> rxPacketWaitRegistry = new List<PacketRegistryEntry>();

        internal Task txTask;
        internal Task rxTask;
        internal Task rxListenerTask;

        internal delegate bool isValidPacket(byte[] bufferArray, out int returnCode, out int packetStart, out int packetLength);
        internal delegate void processPacket(NetPacket packet);

        internal isValidPacket validator;
        internal processPacket processor;

        public TPLTCPQueuedComm(isValidPacket validator, processPacket processor, IPAddress listenerIPAddress, ushort networkPort)
        {
            // Start comms processing tasks
            txTask = Task.Factory.StartNew(x => networkTxTask(clientTokenSource.Token), "networkTxTask", TaskCreationOptions.LongRunning);
            rxTask = Task.Factory.StartNew(x => networkRxTask(clientTokenSource.Token), "networkRxTask", TaskCreationOptions.LongRunning);
            rxListenerTask = Task.Factory.StartNew(x => networkRxListenerTask(listenerTokenSource.Token, listenerIPAddress, networkPort), 
                "networkRxListenerTask", TaskCreationOptions.LongRunning);

            this.validator = validator;
            this.processor = processor;
        }

        #region Public methods
        public bool isListening()
        {
            return rxListenerTask.Status == TaskStatus.Running;
        }

        public bool toggleListen(IPAddress listenerIPAddress, ushort networkPort)
        {
            if (isListening())
            {
                listenerTokenSource.Cancel();
            }
            else
            {
                listenerTokenSource = new CancellationTokenSource();
                rxListenerTask = Task.Factory.StartNew(x => networkRxListenerTask(listenerTokenSource.Token, listenerIPAddress, networkPort), 
                    "networkRxListenerTask", TaskCreationOptions.LongRunning);
            }

            return isListening();
        }
        #endregion

        #region Comms processing methods
        /// <summary>
        /// Responsible for managing the listener socket
        /// </summary>
        /// <param name="cancelToken"></param>
        private void networkRxListenerTask(CancellationToken cancelToken, IPAddress listenerIPAddress, ushort networkPort)
        {
            // TcpListener server = new TcpListener(port);
            TcpListener server = new TcpListener(listenerIPAddress, networkPort);

            // Start listening for client requests.
            server.Start();

            while (!cancelToken.IsCancellationRequested)
            {
                // Perform a blocking call to accept requests.
                // You could also use server.AcceptSocket() here.
                if (server.Pending())
                {
                    TcpClient client = server.AcceptTcpClient();

                    // Hand off the TcpClient to another task to handle any incoming responses
                    Task.Factory.StartNew(x => networkResponseRxTask(client, cancelToken), "networkResponseRxTask: " + client.Client.RemoteEndPoint.ToString());
                }
            }
            server.Stop();
        }

        /// <summary>
        /// Responsible for managing the incoming packet queue
        /// </summary>
        /// <param name="cancelToken"></param>
        private void networkRxTask(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                // Build a wrapper around the tx ConcurrentQueue to allow us to make blocking Take calls
                //BlockingCollection<NetPacket> rxQueue = new BlockingCollection<NetPacket>(inPackets);

                try
                {
                    // Get the next input packet to process, blocking until there is a packet available
                    NetPacket currentPacket = inPackets.Take(listenerTokenSource.Token);

                    // Check if anyone has registered for this response packet
                    // Should only be one listener for each endpoint pair, but who knows, kids these days...
                    IEnumerable<PacketRegistryEntry> matchingEntries = rxPacketWaitRegistry.Where(x => (x.source == currentPacket.source) && (x.destination == currentPacket.destination));
                    if (matchingEntries.Count() > 0)
                    {
                        PacketRegistryEntry entry = matchingEntries.First();

                        // If there actually was someone registered, we need to notify them and give them the packet
                        // They are responsible for removing their registry entry once they've pulled the packet
                        if (entry != null)
                        {
                            entry.packet = currentPacket;
                            entry.packetReady.Set();
                            continue;
                        }
                    }
                    // Otherwise nobody is waiting for this packet, so start it processing straight up
                    processor(currentPacket);
                }
                catch (OperationCanceledException)
                {
                    // Swallow the exception and let the task terminate
                    // As of this writing, there was no other solid solution to this issue
                    //http://stackoverflow.com/questions/8953407/using-blockingcollection-operationcanceledexception-is-there-a-better-way
                }
            }
        }

        /// <summary>
        /// Responsible for managing the outgoing packet queue and responses to outgoing packets
        /// </summary>
        /// <param name="cancelToken"></param>
        private void networkTxTask(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                // Build a wrapper around the tx ConcurrentQueue to allow us to make blocking Take calls
                //BlockingCollection<NetPacket> txQueue = new BlockingCollection<NetPacket>(outPackets);

                try
                {
                    // Get the next packet to send, blocking until there is a packet available
                    NetPacket currentPacket = outPackets.Take(listenerTokenSource.Token);

                    // Connect to the remote host
                    TcpClient tcp = new TcpClient(currentPacket.destination.Address.ToString(), currentPacket.destination.Port);

                    // Write the packet payload to the network stream
                    NetworkStream stream = tcp.GetStream();
                    stream.Write(currentPacket.payload, 0, currentPacket.payload.Length);

                    // Hand off the TcpClient to another task to handle any incoming responses
                    Task.Factory.StartNew(x => networkResponseRxTask(tcp, cancelToken), "networkResponseRxTask: " + tcp.Client.RemoteEndPoint.ToString());
                }
                catch (OperationCanceledException)
                {
                    // Swallow the exception and let the task terminate
                    // As of this writing, there was no other solid solution to this issue
                    //http://stackoverflow.com/questions/8953407/using-blockingcollection-operationcanceledexception-is-there-a-better-way
                }
                catch (SocketException ex)
                {
                    // Swallow for now
                }
            }
        }

        const int networkRxBufferSize = 256;
        /// <summary>
        /// Responsible for handling possible incoming data on each connection. Maintains its own internal incoming buffer and
        /// uses the appropriate packet validation method to determine when it has received a packet. On receipt of a packet, it
        /// adds the packet to the rxPacketQueue for the main RX task to handle.
        /// </summary>
        /// <param name="tcp">TCP client to look for a response on.</param>
        /// <param name="cancelToken">Signals this task to finish any current operation and close <paramref name="tcp"/></param>
        private void networkResponseRxTask(TcpClient tcp, CancellationToken cancelToken)
        {
            // Mark when the task started
            DateTime lastSocketActivityTime = DateTime.Now;

            // Network stream to read/write on
            NetworkStream stream = tcp.GetStream();

            // Buffer to hold the running incoming data stream
            List<byte> rxBuffer = new List<byte>();

            // Temporary buffer to hold the chunk of data read in most recently
            byte[] rxTemp = new byte[networkRxBufferSize];

            // Keep looping through and reading until we either time out or are instructed to cancel
            while (((DateTime.Now - lastSocketActivityTime).TotalMilliseconds < TCPResponseTimeoutMS) && 
                (!cancelToken.IsCancellationRequested))
            {
                // The number of bytes most recently read from the network stream
                int bytesRead = 0;

                try
                {
                    // Stream.Read blocks if no data is available (which we don't want) so check for data
                    // before reading to avoid blocking.
                    if (stream.DataAvailable)
                    {
                        // Try to read in data from the network stream
                        bytesRead = stream.Read(rxTemp, 0, networkRxBufferSize);
                    }
                }
                catch (IOException)
                {
                    // The socket's been forcefully closed on the other end, so we're done here
                    break;
                }
                catch (SocketException)
                {
                    // The socket's been forcefully closed on the other end, so we're done here
                    break;
                }

                if (bytesRead > 0)
                {
                    // Add all the bytes that were read into the temporary buffer into the rxBuffer list 
                    rxBuffer.AddRange(rxTemp.Where((item, index) => index < bytesRead));
                }
                // If we haven't read any data, make sure the socket's still up
                else if (!IsConnected(tcp))
                {
                    break;
                }

                // If the temp buffer wasn't big enough to hold all the incoming data, we need to read
                // again without waiting
                if (bytesRead == networkRxBufferSize)
                {
                    continue;
                }

                // Process new data in the buffer
                // I didn't do this in the earlier if (bytesRead > 0) so we could give the chance to read more data
                // if the incoming buffer's full
                if (bytesRead > 0)
                {
                    int rc;
                    int packetStart;
                    int packetLength;
                    byte[] bufferArray = rxBuffer.ToArray();

                    // If there's a new packet in the input buffer...
                    //if (EncryptedDaedalusPacket.IsValidPacket(bufferArray, out rc, out packetStart, out packetLength))
                    if (validator(bufferArray, out rc, out packetStart, out packetLength))
                    {
                        addNewRxPacket(TransportType.Tcp, tcp.Client.LocalEndPoint, tcp.Client.RemoteEndPoint,
                            inPackets, rxPacketWaitRegistry, bufferArray, packetStart, packetLength);

                        // Remove the packet and all preceding bytes from the input buffer
                        rxBuffer.RemoveRange(0, packetStart + packetLength);
                    }

                    // Reset the timeout
                    lastSocketActivityTime = DateTime.Now;
                }

                // No good way to sleep inside a task... we'll see if this burns too much processor just looping as-is
                // or if we need to set up a way to asynchronously wait on incoming data
                //Sleep();
            }

            // If we get here we've either timed out or been ordered to cancel
            stream.Close();
            tcp.Close();
        }

        /// <summary>
        /// Send a poll to check if a connection is still active. This is somewhat difficult due to the structure of TCP sockets, i.e.
        /// http://nitoprograms.blogspot.com/2009/05/detection-of-half-open-dropped.html
        /// </summary>
        /// <param name="socket">Socket to check status of.</param>
        /// <returns>True if the socket's still alive and connected, false otherwise.</returns>
        private bool IsConnected(TcpClient client)
        {
            try
            {
                return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
            }
            catch (SocketException) { return false; }
        }
    

        private void addNewRxPacket(TransportType transport, EndPoint d, EndPoint s,
            BlockingCollection<NetPacket> rxPacketQueue, List<PacketRegistryEntry> rxPacketRegistry,
            byte[] payloadBuffer, int packetStart, int packetLength)
        {
            byte[] newPayload = new byte[packetLength - packetStart];
            Array.Copy(payloadBuffer, packetStart, newPayload, 0, packetLength);

            rxPacketQueue.Add(new NetPacket
            {
                destination = (IPEndPoint)d,
                source = (IPEndPoint)s,
                payload = newPayload,
                transportType = transport
            });
        }
        #endregion

        #region IDisposable
        private bool disposed = false;

        //Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects).
                }
                // Free your own state (unmanaged objects).
                listenerTokenSource.Cancel();
                clientTokenSource.Cancel();
                // Set large fields to null.
                disposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~TPLTCPQueuedComm()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }
        #endregion
    }
}
