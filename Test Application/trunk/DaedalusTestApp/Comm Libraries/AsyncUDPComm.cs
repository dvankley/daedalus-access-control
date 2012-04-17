using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace NetworkHelpers
{
    public class AsyncUDPComm : AsyncSingleComm, IDisposable
    {
        #region Instance variables
        Socket socket;
        object socketLocker = new object();
        bool socketDisposed = true;
        List<byte> inputBuffer = new List<byte>();
        IPEndPoint lastRemoteEndPoint;

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
        /// Constructor for a packet-type Async UDP Comm object
        /// </summary>
        /// <param name="packetBufferSize">Size for each packet's receive buffer. Should be set to the max packet size that the client may return.
        /// This is not the max size of the total input buffer, which is resized as needed.</param>
        /// <param name="validatePacketDelegate">Delegate to verify that a given byte array is a valid packet for this particular application.</param>
        /// <param name="receivedPacketDelegate">Delegate to handle verified received packets for this application.</param>
        /// <param name="socketClosedDelegate">Delegate to be notified when we're in a server recieve mode and the other side disconnects.</param>
        /// <param name="commExceptionDelegate">Delegate to pass exceptions to when thrown in async methods.</param>
        public AsyncUDPComm(uint packetBufferSize, ValidatePacketDelegate validatePacketDelegate,
            ReceivedPacketDelegate receivedPacketDelegate, SocketCloseDelegate socketCloseDelegate, CommExceptionDelegate commExceptionDelegate)
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
            this.commExceptionDelegate = commExceptionDelegate;
            this.socketCloseDelegate = socketCloseDelegate;
        }

        /// <summary>
        /// Constructor for a stream-type Async UDP Comm object
        /// </summary>
        /// <param name="packetBufferSize">Size for each packet's receive buffer. Should be set to the max packet size that the client may return.
        /// This is not the max size of the total input buffer, which is resized as needed.</param>
        /// <param name="receivedStreamDataDelegate">Delegate to handle reading the input UDP stream when data is received. 
        /// Delegate is also required to clear the input buffer when possible.</param>
        /// <param name="socketClosedDelegate">Delegate to be notified when we're in a server recieve mode and the other side disconnects.</param>
        /// <param name="commExceptionDelegate">Delegate to pass exceptions to when thrown in async methods.</param>
        public AsyncUDPComm(uint packetBufferSize, ReceivedStreamDataDelegate receivedStreamDataDelegate,
            SocketCloseDelegate socketCloseDelegate, CommExceptionDelegate commExceptionDelegate)
        {
            if (receivedStreamDataDelegate == null)
            {
                throw new ArgumentNullException("receivedStreamDataDelegate");
            }
            this.packetBufferSize = packetBufferSize;
            this.commMode = CommMode.Stream;
            this.receivedStreamDataDelegate = receivedStreamDataDelegate;
            this.commExceptionDelegate = commExceptionDelegate;
            this.socketCloseDelegate = socketCloseDelegate;
        }
        #endregion

        #region Public methods
        /// <summary>
        /// <para>Begins receiving on the specified local end point for remote data.</para>
        /// </summary>
        /// <param name="localEndPoint">The local end point (IP address and port) to listen for connections on.</param>
        /// <returns>True if successful, false otherwise.</returns>
        /// <exception cref="AsyncCommException">Thrown to indicate a general exception with the AsyncComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public override bool Listen(IPEndPoint localEndPoint)//, IPEndPoint remoteEndPoint)
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

            lock (socketLocker)
            {
                if (!socketDisposed)
                {
                    socket.Close(2);
                }
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socketDisposed = false;
                socket.ExclusiveAddressUse = false;
                socket.Bind(localEndPoint);

                byte[] packet = new byte[packetBufferSize];                                             // Create a new byte buffer to store the incoming data              

                // Listen for data from anywhere
                EndPoint tempEndPoint = anyEndPoint;
                socket.BeginReceiveFrom(packet, 0, (int)packetBufferSize, SocketFlags.None, ref tempEndPoint, new AsyncCallback(UDPDataReceive), packet);                
            }
            return true;
        }

        /// <summary>
        /// <para>Starts an async operation to send <paramref name="data"/> to <paramref name="remoteEndPoint"/></para>
        /// <para>Currently this only supports sending from a random local port to maintain the same signature as the AsyncTCPComm version.</para>
        /// </summary>
        /// <param name="remoteEndPoint">The end point to send <paramref name="data"/> to.</param>
        /// <param name="data">The data buffer to send to <paramref name="remoteEndPoint"/></param>
        /// <returns>True if successful, false otherwise.</returns>
        /// <exception cref="AsyncCommException">Thrown to indicate a general exception with the AsyncComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public override bool Send(IPEndPoint remoteEndPoint, byte[] data)
        {
            lock (socketLocker)
            {
                if (socketDisposed)
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);  // Create a new socket in the same slot
                    socketDisposed = false;                   
                }
                socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, remoteEndPoint, new AsyncCallback(UDPClientSend), socket);
                lastRemoteEndPoint = remoteEndPoint;
            }
            return true;
        }

        /// <summary>
        /// Closes and disposes of all resources of this comm object. IsClosed and IsDisposed are sent when finished. Synonymous with Dispose().
        /// </summary>
        /// <exception cref="AsyncCommException">Thrown to indicate a general exception with the AsyncComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public override void Close()
        {
            Dispose(true);
        }

        /// <summary>
        /// Closes and disposes of all resources of this comm object. IsClosed and IsDisposed are sent when finished. Synonymous with Close().
        /// </summary>
        /// <exception cref="AsyncCommException">Thrown to indicate a general exception with the AsyncComm class.</exception>
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
            // Lock both sockets at the same time to keep either from changing while we're messing with them
            lock (socketLocker)
            {
                if (!socketDisposed)
                {
                    if (disposing)
                    {
                        if (socket != null)
                            //socket.Dispose(); Can't do this because of some accessibility goofiness
                            socket.Close(2);
                    }

                    // Indicate that the instance has been disposed.
                    socket = null;
                    socketDisposed = true;
                }
            }
        }
        #endregion

        #region Public properties
        /// <summary>
        /// The remote end point for this comm object. Currently this just returns null because this is unreliable information for UDP.
        /// </summary>
        public override IPEndPoint remoteEndPoint
        {
            get
            {
                try
                {
                    lock (socketLocker)
                    {
                        return lastRemoteEndPoint;
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// <para>Indicates if the comm object's socket has been disposed.</para>
        /// <para>Synonymous with IsClosed</para>
        /// </summary>
        public override bool IsDisposed
        {
            get
            {
                lock (socketLocker)
                {
                    return socketDisposed;
                }
            }
        }

        /// <summary>
        /// <para>Indicates if the comm object's socket has been disposed.</para>
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
        /// Always returns true to maintain interop with AsyncTCPComm.
        /// </summary>
        public override bool IsConnected
        {
            get { return true; }
        }

        public override string ToString()
        {
            if (this.remoteEndPoint == null)
            {
                return "AsyncUDPComm : ";
            }
            else
            {
                return "AsyncUDPComm : " + remoteEndPoint.ToString();
            }
        }
        #endregion

        #region UDP socket async callbacks
        /// <summary>
        /// Server data receive callback. Handles incoming data.
        /// </summary>
        /// <param name="asyn">Asynch state object. The byte buffer in which to store the received data.</param>
        /// <returns>Nothing.</returns>
        private void UDPDataReceive(IAsyncResult asyn)
        {
            try
            {
                lock (socketLocker)
                {
                    // This method gets called when the socket's disposed, for some reason, 
                    // so we need to make sure we don't work with a disposed socket
                    if (!socketDisposed)
                    {
                        if (asyn.AsyncState is byte[])                                                  // Verify wrapper object is byte[]
                        {
                            byte[] rawBuffer = (byte[])asyn.AsyncState;                                     // Unwrap local packet object from async state wrapper

                            // Store the number of bytes received by the socket
                            EndPoint tempEndPoint = anyEndPoint;
                            int receivedDataLength = socket.EndReceiveFrom(asyn, ref tempEndPoint);
                            lastRemoteEndPoint = (IPEndPoint)tempEndPoint;
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


                                //byte[] responsePacket = new byte[packetBufferSize];
                                //dataSocket.BeginReceive(responsePacket, 0, (int)packetBufferSize,               // Call this function again to continue receiving data from the remote host
                                //    SocketFlags.None, new AsyncCallback(TCPDataReceive), responsePacket);
                                byte[] responsePacket = new byte[packetBufferSize];
                                tempEndPoint = anyEndPoint;
                                socket.BeginReceiveFrom(responsePacket, 0, (int)packetBufferSize, SocketFlags.None, ref tempEndPoint, new AsyncCallback(UDPDataReceive), responsePacket);

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
        /// Client send data callback. Sends outgoing data.
        /// </summary>
        /// <param name="asyn">Asynch state object. The byte buffer of data to send.</param>
        /// <returns>Nothing.</returns>
        private void UDPClientSend(IAsyncResult asyn)
        {
            try
            {
                lock (socketLocker)
                {
                    if (asyn.AsyncState is byte[])                                                  // Verify wrapper object is byte[]
                    {
                        int bytesSent = 0;                                                              // Holds the number of bytes output by this send operation

                        byte[] packetOut = (byte[])asyn.AsyncState;                                 // Unwrap local packet object from async state wrapper

                        bytesSent = socket.EndSendTo(asyn);                                       // Save the number of bytes sent in this send operation
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
