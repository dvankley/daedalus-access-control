using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using GlobalHelpers;

namespace NetworkHelpers
{
    public class AsyncTCPComm : AsyncSingleComm, IDisposable
    {
        #region Instance variables
        Socket listenerSocket;
        Socket dataSocket;
        object listenerSocketLocker = new object();
        object dataSocketLocker = new object();
        bool listenerSocketDisposed = true;
        bool dataSocketDisposed = true;
        List<byte> inputBuffer = new List<byte>();

        object checkConnectionTimerLocker = new object();
        System.Threading.Timer checkConnectionTimer;

        readonly uint packetBufferSize;
        readonly CommMode commMode;
        readonly ReceivedPacketDelegate receievedPacketDelegate;
        readonly ValidatePacketDelegate validatePacketDelegate;
        readonly ReceivedStreamDataDelegate receivedStreamDataDelegate;
        readonly SocketCloseDelegate socketCloseDelegate;
        readonly CommExceptionDelegate commExceptionDelegate;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor for a packet-type Async TCP Comm object
        /// </summary>
        /// <param name="packetBufferSize">Size for each packet's receive buffer. Should be set to the max packet size that the client may return.
        /// This is not the max size of the total input buffer, which is resized as needed.</param>
        /// <param name="validatePacketDelegate">Delegate to verify that a given byte array is a valid packet for this particular application.</param>
        /// <param name="receivedPacketDelegate">Delegate to handle verified received packets for this application.</param>
        /// <param name="socketClosedDelegate">Delegate to be notified when we're in a server recieve mode and the other side disconnects.</param>
        /// <param name="commExceptionDelegate">Delegate to pass exceptions to when thrown in async methods.</param>
        public AsyncTCPComm(uint packetBufferSize, ValidatePacketDelegate validatePacketDelegate,
            ReceivedPacketDelegate receivedPacketDelegate, SocketCloseDelegate socketClosedDelegate, CommExceptionDelegate commExceptionDelegate)
        {
            if (validatePacketDelegate == null)
            {
                throw new ArgumentNullException("validatePacketDelegate");
            }
            if (receivedPacketDelegate == null)
            {
                throw new ArgumentNullException("receivedPacketDelegate");
            }
            this.packetBufferSize = packetBufferSize;
            this.commMode = CommMode.Packet;
            this.receievedPacketDelegate = receivedPacketDelegate;
            this.validatePacketDelegate = validatePacketDelegate;
            this.socketCloseDelegate = socketClosedDelegate;
            this.commExceptionDelegate = commExceptionDelegate;
        }

        /// <summary>
        /// Constructor for a stream-type Async TCP Comm object
        /// </summary>
        /// <param name="packetBufferSize">Size for each packet's receive buffer. Should be set to the max packet size that the client may return.
        /// This is not the max size of the total input buffer, which is resized as needed.</param>
        /// <param name="receivedStreamDataDelegate">Delegate to handle reading the input TCP stream when data is received. 
        /// Delegate is also required to clear the input buffer when possible.</param>
        /// <param name="socketClosedDelegate">Delegate to be notified when we're in a server recieve mode and the other side disconnects.</param>
        /// <param name="commExceptionDelegate">Delegate to pass exceptions to when thrown in async methods.</param>
        public AsyncTCPComm(uint packetBufferSize, ReceivedStreamDataDelegate receivedStreamDataDelegate,
            SocketCloseDelegate socketClosedDelegate, CommExceptionDelegate commExceptionDelegate)
        {
            if (receivedStreamDataDelegate == null)
            {
                throw new ArgumentNullException("receivedStreamDataDelegate");
            }
            this.packetBufferSize = packetBufferSize;
            this.commMode = CommMode.Stream;
            this.receivedStreamDataDelegate = receivedStreamDataDelegate;
            this.socketCloseDelegate = socketClosedDelegate;
            this.commExceptionDelegate = commExceptionDelegate;
        }
        
        #endregion

        #region Public methods
        /// <summary>
        /// <para>Begins listening on the specified local end point for remote connections.</para>
        /// <para>This class currently only supports one data connection at a time. Further connection requests will drop the first.</para>
        /// </summary>
        /// <param name="localEndPoint">The local end point (IP address and port) to listen for connections on.</param>
        /// <returns>True if successful, false otherwise.</returns>
        /// <exception cref="AsyncTCPCommException">Thrown to indicate a general exception with the AsyncTCPComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public override bool Listen(IPEndPoint localEndPoint)
        {
            IPHostEntry thisHost = Dns.GetHostEntry(Dns.GetHostName()); 

            if (thisHost.AddressList.Length < 1)
            {
                throw new AsyncCommException("Failed to begin listening, this host has no ethernet interfaces active.");
            }
            if (localEndPoint == null)
            {
                throw new ArgumentNullException("localEndPoint");
            }
            if (!thisHost.AddressList.Contains(localEndPoint.Address))
            {
                throw new AsyncCommException("Failed to begin listening, localEndPoint is not an address on this host's list of interfaces.");
            }

            lock (listenerSocketLocker)
            {
                if (!listenerSocketDisposed)
                {
                    listenerSocket.Close(2);
                }
                listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listenerSocketDisposed = false;
                listenerSocket.ExclusiveAddressUse = false;
                listenerSocket.Bind(localEndPoint);
                listenerSocket.Listen(pendingServerConnectionBacklog);
                listenerSocket.BeginAccept(new AsyncCallback(TCPServerConnectionAccept), listenerSocket);
            }
            return true;
        }

        /// <summary>
        /// <para>Starts an async operation to send <paramref name="data"/> to <paramref name="remoteEndPoint"/></para>
        /// <para>Will connect to the end point if a connection does not already exist.</para>
        /// <para>If a connection to the specified end point already exists, whether from a previous send operation or from an incoming connection, the data will be sent on that connection.</para>
        /// <para>If a previous connection to anywhere but the specified end point already exists, it will be dropped and a connection to the specified end point initiated.</para>
        /// </summary>
        /// <param name="remoteEndPoint">The end point to send <paramref name="data"/> to.</param>
        /// <param name="data">The data buffer to send to <paramref name="remoteEndPoint"/></param>
        /// <returns>True if successful, false otherwise.</returns>
        /// <exception cref="AsyncTCPCommException">Thrown to indicate a general exception with the AsyncTCPComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public override bool Send(IPEndPoint remoteEndPoint, byte[] data)
        {
            lock (dataSocketLocker)
            {
                if (dataSocketDisposed)
                {
                    dataSocket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);  // Create a new socket in the same slot
                    dataSocketDisposed = false;
                    dataSocket.BeginConnect(remoteEndPoint, new AsyncCallback(TCPClientConnect), data);   // Begin async connection to the remote host  
                }
                else
                {
                    if (dataSocket.IsConnected() && (dataSocket.RemoteEndPoint == remoteEndPoint))
                    {
                        dataSocket.BeginSend(data, 0, // Begin async data transmit
                                        data.Length, SocketFlags.None, new AsyncCallback(TCPClientSend), data);
                    }
                    else
                    {
                        dataSocket.Close(2);                                       // Close the socket connection
                        dataSocket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);  // Create a new socket in the same slot
                        dataSocketDisposed = false;
                        dataSocket.BeginConnect(remoteEndPoint, new AsyncCallback(TCPClientConnect), data);   // Begin async connection to the remote host   
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Closes and disposes of all resources of this comm object. IsClosed and IsDisposed are sent when finished. Synonymous with Dispose().
        /// </summary>
        /// <exception cref="AsyncTCPCommException">Thrown to indicate a general exception with the AsyncTCPComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public override void Close()
        {
            Dispose(true);
        }

        /// <summary>
        /// Closes and disposes of all resources of this comm object. IsClosed and IsDisposed are sent when finished. Synonymous with Close().
        /// </summary>
        /// <exception cref="AsyncTCPCommException">Thrown to indicate a general exception with the AsyncTCPComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public override void Dispose()
        {
            Dispose(true);
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Canonical dispose method modelled off MSDN's example
        /// Disposes of both of the sockets and sets flags accordingly so the async processes don't try
        /// to mess with the disposed objects.
        /// </summary>
        /// <param name="disposing">I don't actually know what this is for. Should never be false.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Disable the periodic connection check timer
            stopCheckingConnection();

            // Lock both sockets at the same time to keep either from changing while we're messing with them
            lock (listenerSocketLocker)
            {
                lock (dataSocketLocker)
                {
                    if (!listenerSocketDisposed)
                    {
                        if (disposing)
                        {
                            if (listenerSocket != null)
                                //socket.Dispose(); Can't do this because of some accessibility goofiness
                                listenerSocket.Close(2);
                        }

                        // Indicate that the instance has been disposed.
                        listenerSocket = null;
                        listenerSocketDisposed = true;
                    }

                    if (!dataSocketDisposed)
                    {
                        if (disposing)
                        {
                            if (dataSocket != null)
                                //socket.Dispose(); Can't do this because of some accessibility goofiness
                                dataSocket.Close(2);
                        }

                        // Indicate that the instance has been disposed.
                        dataSocket = null;
                        dataSocketDisposed = true;
                    }
                }
            }
        }

        /// <summary>
        /// <para>Checks the status of the comm object's data socket and notifies the parent object's delegate if it's no longer connected.</para> 
        /// </summary>
        /// <param name="state">Null</param>
        void asyncCheckConnectionTimerElapsed(object state)
        {
            try
            {
                lock (dataSocketLocker)
                {
                    // If the data socket's not disposed then check it's status...                    
                    if (!dataSocketDisposed)
                    {
                        // Poll the socket to see if it's still connected
                        if (!dataSocket.IsConnected())
                        {
                            // If the socket's not connected any more, notify the constructing object's delegate
                            socketCloseDelegate(SocketError.NotConnected, this, (IPEndPoint)dataSocket.RemoteEndPoint);
                            // And stop this timer
                            stopCheckingConnection();
                        }
                    }
                    // Otherwise if the data socket's disposed, then we shouldn't be running at all
                    else
                    {
                        stopCheckingConnection();
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Stops the checkConnectionTimer and sets it to null so it will be properly GC'ed.
        /// Thread safe.
        /// </summary>
        private void stopCheckingConnection()
        {
            lock (checkConnectionTimerLocker)
            {
                if (checkConnectionTimer != null)
                {
                    checkConnectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    checkConnectionTimer.Dispose();
                    checkConnectionTimer = null;
                }
            }
        }

        /// <summary>
        /// Convenience method to check if checkConnectionTimer is currently running, i.e. if we think we're supposed to be connected right now.
        /// </summary>
        /// <returns></returns>
        private bool areCheckingConnection()
        {
            lock (checkConnectionTimerLocker)
            {
                return checkConnectionTimer != null;
            }
        }
        #endregion

        #region Public properties
        /// <summary>
        /// The remote end point for this comm object's dataSocket. Null if not connected or instantiated.
        /// </summary>
        public override IPEndPoint remoteEndPoint
        {
            get
            {
                try
                {
                    lock (dataSocketLocker)
                    {
                        if (!dataSocketDisposed)
                        {
                            return (IPEndPoint)dataSocket.RemoteEndPoint;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// <para>Indicates if the comm object (specifically, both its sockets) has been disposed.</para>
        /// <para>Synonymous with IsClosed</para>
        /// </summary>
        public override bool IsDisposed
        {
            get
            {
                lock (listenerSocketLocker)
                {
                    lock (dataSocketLocker)
                    {
                        return (dataSocketDisposed && listenerSocketDisposed);
                    }
                }
            }
        }

        /// <summary>
        /// <para>Indicates if the comm object (specifically, both its sockets) has been disposed.</para>
        /// <para>Synonymous with IsDisposed</para>
        /// </summary>
        public override bool IsClosed
        {
            get
            {
                return IsDisposed;
            }
        }

        /// <summary>
        /// <para>Indicates if the comm object's data socket is currently connected.</para>
        /// <para>This is based on active polling of the connection so should be accurate to the actual state of the socket.</para>
        /// </summary>
        public override bool IsConnected
        {
            get
            {
                // This timer should always be running if we're actually connected to something
                lock (checkConnectionTimerLocker)
                {
                    return checkConnectionTimer != null;
                }
            }
        }

        public override string ToString()
        {
            return "AsyncTCPComm : " + remoteEndPoint.ToString();
        }
        #endregion

        #region TCP socket async callbacks
        /// <summary>
        /// Server connection accept callback. Handles incoming connection requests.
        /// </summary>
        /// <param name="asyn">Asynch state object.</param>
        /// <returns>Nothing.</returns>
        private void TCPServerConnectionAccept(IAsyncResult asyn)
        {
            try
            {
                // I tried this individually locking each socket as needed, but it gets goofy when you try to dispose the socket,
                // so I just went with taking out both locks for any code section that uses both sockets
                lock (listenerSocketLocker)
                {
                    lock (dataSocketLocker)
                    {
                        if (!listenerSocketDisposed)
                        {
                            if (dataSocketDisposed)
                            {
                                // Pick up the new socket connection
                                dataSocket = listenerSocket.EndAccept(asyn);                           // Complete the connection accept operation
                                dataSocketDisposed = false;
                            }
                            else
                            {
                                if (areCheckingConnection())
                                {
                                    stopCheckingConnection();
                                }
                                dataSocket.Close(2);
                                dataSocket = listenerSocket.EndAccept(asyn);
                                dataSocketDisposed = false;

                            }
                            listenerSocket.BeginAccept(new AsyncCallback(TCPServerConnectionAccept), listenerSocket);   // Begin a new async accept operation
                            byte[] packet = new byte[packetBufferSize];                                             // Create a new packet to store the incoming data

                            dataSocket.BeginReceive(packet, 0, (int)packetBufferSize,
                                        SocketFlags.None, new AsyncCallback(TCPDataReceive), packet);             // Start async receive operation, put received data into packet.packetBuffer

                            // Start periodically checking this connection to see if it's still connected
                            lock (checkConnectionTimerLocker)
                            {
                                checkConnectionTimer = new Timer(new TimerCallback(asyncCheckConnectionTimerElapsed), null, 0, checkConnectionIntervalMilliseconds);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                commExceptionDelegate(e, this);
            }
        }

        /// <summary>
        /// Server data receive callback. Handles incoming data.
        /// </summary>
        /// <param name="asyn">Asynch state object. The byte buffer in which to store the received data.</param>
        /// <returns>Nothing.</returns>
        private void TCPDataReceive(IAsyncResult asyn)
        {
            try
            {
                lock (dataSocketLocker)
                {
                    // This method gets called when the socket's disposed, for some reason, 
                    // so we need to make sure we don't work with a disposed socket
                    if (!dataSocketDisposed)
                    {
                        if (asyn.AsyncState is byte[])                                                  // Verify wrapper object is byte[]
                        {
                            byte[] rawBuffer = (byte[])asyn.AsyncState;                                     // Unwrap local packet object from async state wrapper
                            if (dataSocket.IsConnected())                                                       // If this connection's socket is still connected...
                            // It's worth noting that the "connected" property only gets set to false if the connection is closed on this end.
                            {
                                // Store the number of bytes recieved by the socket
                                int receivedDataLength = dataSocket.EndReceive(asyn);
                                if (receivedDataLength == 0)                                                    // If EndReceive returned 0, the remote host has closed the connection                                        
                                {
                                    return;                                                                         // Nothing else for this function to do, so return
                                }
                                else
                                {
                                    // Packet buffer to process
                                    byte[] totalPacketData;
                                    // Copy the received bytes and everything remaining in the input buffer into an array to pass to the delegate
                                    lock (inputBuffer)
                                    {
                                        totalPacketData = new byte[receivedDataLength + inputBuffer.Count];
                                        // Copy anything lingering in the input buffer into the packet buffer to process
                                        Array.Copy(inputBuffer.ToArray(), totalPacketData, inputBuffer.Count);
                                        // Copy the just received data into the end of the packet buffer to process as well
                                        Array.Copy(rawBuffer, 0, totalPacketData, inputBuffer.Count, receivedDataLength);
                                    }

                                    if (commMode == CommMode.Packet)
                                    {
                                        // Validate the packet using the delegate we were passed at construction
                                        ValidatePacketResponse validateResponse = validatePacketDelegate(totalPacketData, (uint)receivedDataLength, this, this.remoteEndPoint);
                                        // If the delegate flagged the packet as valid...
                                        if (validateResponse.packetIsValid)
                                        {
                                            // Copy just the packet data into an output buffer
                                            byte[] returnData = new byte[validateResponse.packetLength];
                                            Array.Copy(totalPacketData, validateResponse.packetStartIndex, returnData, 0, validateResponse.packetLength);
                                            // Pass the validated packet data back using a delegate
                                            receievedPacketDelegate(returnData, this, this.remoteEndPoint);

                                            // If we've seen a valid packet, anything remaining in the input buffer can be flushed
                                            lock (inputBuffer)
                                            {
                                                inputBuffer.Clear();
                                            }

                                            // If the validated packet does not extend to the end of the packet data, 
                                            // we need to copy the extra into the input buffer for later use
                                            uint validatedEndIndex = validateResponse.packetStartIndex + validateResponse.packetLength;

                                            if (validatedEndIndex < totalPacketData.Length)
                                            {
                                                byte[] leftovers = new byte[totalPacketData.Length - validatedEndIndex];
                                                Array.Copy(totalPacketData, validatedEndIndex, leftovers, 0, totalPacketData.Length - validatedEndIndex);
                                                lock (inputBuffer)
                                                {
                                                    inputBuffer.AddRange(leftovers);
                                                }
                                            }
                                        }
                                        // Otherwise the delegate rejected the packet, so we need to hang onto the new packet data in case a piece of it is needed later.
                                        else
                                        {
                                            lock (inputBuffer)
                                            {
                                                byte[] rawReceivedData = new byte[receivedDataLength];
                                                Array.Copy(rawBuffer, rawReceivedData, receivedDataLength);

                                                inputBuffer.AddRange(rawReceivedData);
                                            }
                                        }
                                    }
                                    else if (commMode == CommMode.Stream)
                                    {
                                        lock (inputBuffer)
                                        {
                                            // Add the received data to the total input stream data
                                            inputBuffer.AddRange(totalPacketData);
                                            // Pass a reference to the input stream data to the delegate we were passed at construction
                                            receivedStreamDataDelegate(inputBuffer, this, this.remoteEndPoint);
                                        }
                                    }

                                    if (dataSocket.IsConnected())                                                       // If the socket's still connected on this end...
                                    {
                                        byte[] responsePacket = new byte[packetBufferSize];
                                        dataSocket.BeginReceive(responsePacket, 0, (int)packetBufferSize,               // Call this function again to continue receiving data from the remote host
                                            SocketFlags.None, new AsyncCallback(TCPDataReceive), responsePacket);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (ArgumentException e)
            {
                commExceptionDelegate(new AsyncCommException("Received new connection on already connected server socket. " +
                    "This comm class currently only supports one data connection at a time. Dropping old connection in favor of new one.", e), this);
            }
            catch (Exception e)
            {
                commExceptionDelegate(e, this);
            }
        }

        /// <summary>
        /// Client connection callback. Creates a connection to a remote host.
        /// </summary>
        /// <param name="asyn">Asynch state object. The byte buffer of data to send.</param>
        /// <returns>Nothing.</returns>
        private void TCPClientConnect(IAsyncResult asyn)
        {
            try
            {
                lock (dataSocketLocker)
                {
                    if (asyn.AsyncState is byte[])                                                  // Verify wrapper object is byte[]
                    {
                        byte[] packetOut = (byte[])asyn.AsyncState;                                     // Unwrap local packet object from async state wrapper
                        dataSocket.EndConnect(asyn);                                                    // Finish the async connect operation

                        // Start periodically checking this connection to see if it's still connected
                        lock (checkConnectionTimerLocker)
                        {
                            checkConnectionTimer = new Timer(new TimerCallback(asyncCheckConnectionTimerElapsed), null, 0, checkConnectionIntervalMilliseconds);
                        }

                        byte[] packetRemoteResponse = new byte[packetBufferSize];                       // Create a new packet object to hold the remote host's response
                        dataSocket.BeginReceive(packetRemoteResponse, 0, (int)packetBufferSize,         // Call this function again to continue receiving data from the remote host
                            SocketFlags.None, new AsyncCallback(TCPDataReceive), packetRemoteResponse);
                        dataSocket.BeginSend(packetOut, 0, packetOut.Length,                            // Begin async data transmit of the original packet
                            SocketFlags.None, new AsyncCallback(TCPClientSend), packetOut);
                    }
                }
            }
            catch (SocketException e)
            {
                switch (e.SocketErrorCode)
                {
                    // Connection actively refused
                    case SocketError.ConnectionRefused:
                        socketCloseDelegate(SocketError.ConnectionRefused, this, this.remoteEndPoint);
                        stopCheckingConnection();
                        break;
                    // Connection reset
                    case SocketError.ConnectionReset:
                        socketCloseDelegate(SocketError.ConnectionReset, this, this.remoteEndPoint);
                        stopCheckingConnection();
                        break;
                    // Who knows
                    default:
                        commExceptionDelegate(e, this);  
                        break;
                }
            }
            catch (Exception e)
            {
                commExceptionDelegate(e, this);
            }
        }

        /// <summary>
        /// Client send data callback. Sends outgoing data.
        /// </summary>
        /// <param name="asyn">Asynch state object. The byte buffer of data to send.</param>
        /// <returns>Nothing.</returns>
        private void TCPClientSend(IAsyncResult asyn)
        {
            try
            {
                lock (dataSocketLocker)
                {
                    if (asyn.AsyncState is byte[])                                                  // Verify wrapper object is TCPPacket
                    {
                        int bytesSent = 0;                                                              // Holds the number of bytes output by this send operation
                        // This should probably be used to call this Send operation again if the bytes out is less than the length of the send buffer, but I haven't tested
                        // if the TCP stack handles fragmentation itself or not yet

                        byte[] packetOut = (byte[])asyn.AsyncState;                                 // Unwrap local packet object from async state wrapper

                        bytesSent = dataSocket.EndSend(asyn);                                       // Save the number of bytes sent in this send operation
                    }
                }
            }
            catch (Exception e)
            {
                commExceptionDelegate(e, this);
            }
        }
        #endregion
    }
}
