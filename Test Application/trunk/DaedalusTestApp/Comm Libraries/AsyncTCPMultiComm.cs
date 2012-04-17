using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using GlobalHelpers;

namespace NetworkHelpers
{
    /// <summary>
    /// A variant of AsyncTCPComm that allows an arbitrary number of data socket connections to
    /// remote hosts.
    /// </summary>
    public class AsyncTCPMultiComm : AsyncMultiComm, IDisposable
    {
        #region Helper classes
        // Would have made these structs but they've got reference types in 'em
        /// <summary>
        /// Helper class to hold all the relevant information about a TCP socket connection, which includes the 
        /// socket itself, a connection checking timer, and locking objects.
        /// </summary>
        private class SocketInfo : IDisposable
        {
            /// <summary>
            /// Main socket for this SocketInfo object.
            /// </summary>
            internal Socket socket;
            //internal bool socketIsDisposed;
            /// <summary>
            /// Used to control access to the socket object.
            /// </summary>
            internal object socketLocker = new object();

            /// <summary>
            /// <para>Used to check if the corresponding socket is connected and notify the parent class if not.</para>
            /// <para>Should only be active while the socket is expected to be connected.</para>
            /// </summary>
            internal Timer checkConnectionTimer;
            /// <summary>
            /// Used to control access to the checkConnectionTimer object.
            /// </summary>
            internal object checkConnectionTimerLocker = new object();

            /// <summary>
            /// Buffer to hold received socket data. Locked directly for multithreading.
            /// </summary>
            internal readonly List<byte> inputBuffer = new List<byte>();

            /// <summary>
            /// Flags if this SocketInfo object has been disposed or not.
            /// </summary>
            private bool isDisposed;

            internal SocketInfo(Socket socket)
            {
                this.socket = socket;
                this.isDisposed = false;
            }

            /// <summary>
            /// Closes and disposes of all resources of this SocketInfo object.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
            }

            protected virtual void Dispose(bool disposing)
            {
                lock (socketLocker)
                {
                    lock (checkConnectionTimerLocker)
                    {
                        if (socket != null)
                        {
                            socket.Close(socketCloseTimeoutMilliseconds);
                            //socketIsDisposed = true;
                            socket = null;
                        }

                        if (checkConnectionTimer != null)
                        {
                            checkConnectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
                            checkConnectionTimer.Dispose();
                            checkConnectionTimer = null;
                        }
                        isDisposed = true;
                    }
                }
            }

            public bool IsDisposed
            {
                get
                {
                    return isDisposed;
                }
            }
        }

        /// <summary>
        /// Wrapper to hold the current callback's SocketInfo and packetBuffer data.
        /// </summary>
        private class AsyncTCPStateData
        {
            public SocketInfo socketInfo;
            public byte[] packetBuffer;

            internal AsyncTCPStateData(SocketInfo socketInfo, byte[] packetBuffer)
            {
                this.socketInfo = socketInfo;
                this.packetBuffer = packetBuffer;
            }
        }

        private class AsyncCheckConnectionStateData
        {
            public SocketInfo socketInfo;
            public IPEndPoint remoteEndPoint;

            internal AsyncCheckConnectionStateData(SocketInfo socketInfo, IPEndPoint remoteEndPoint)
            {
                this.socketInfo = socketInfo;
                this.remoteEndPoint = remoteEndPoint;
            }
        }
        #endregion

        #region Instance variables
        /// <summary>
        /// <para>Each socket is uniquely identified by its remote end point.</para>
        /// <para>The benefit is that response packets can easily be sent over existing connections.</para>
        /// <para>The limitation is that only a single connection is allowed from this machine to a given remote end point.</para>
        /// <para>Locking: this collection's locker object is locked for collection actions (add, read, delete, etc.). 
        /// Individual entries should have their component lock objects locked for socket, timer, etc. operations.</para>
        /// </summary>
        readonly Dictionary<IPEndPoint, SocketInfo> dataSockets = new Dictionary<IPEndPoint, SocketInfo>();

        /// <summary>
        /// Used to control access to the dataSockets object.
        /// </summary>
        object dataSocketsLocker = new object();

        /// <summary>
        /// <para>Single socket used to wait for incoming connection requests.</para>
        /// <para>When a connection request is received, the connected socket is added to dataSockets and this socket continues listening.</para>
        /// </summary>
        Socket listenerSocket;

        /// <summary>
        /// Used to control access to the listenerSocket object.
        /// </summary>
        object listenerSocketLocker = new object();

        /// <summary>
        /// Used to flag if listenerSocket has been disposed or not.
        /// </summary>
        bool listenerSocketDisposed = true;



        //object checkConnectionTimerLocker = new object();
        //volatile System.Threading.Timer checkConnectionTimer;

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
        public AsyncTCPMultiComm(uint packetBufferSize, ValidatePacketDelegate validatePacketDelegate,
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
        public AsyncTCPMultiComm(uint packetBufferSize, ReceivedStreamDataDelegate receivedStreamDataDelegate,
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
                    listenerSocket.Close(socketCloseTimeoutMilliseconds);
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
            // Connections are uniquely identified by their local/remote end point pair. If a connection 
            // exists to a given remote end point, the corresponding local end point should be irrelevant. Therefore, 
            // we will always send on an existing connection to a given remote end point if such a connection is available.

            SocketInfo outputSocketInfo;

            lock (dataSocketsLocker)
            {
                // If a current socket pointing to the desired remoteEndPoint is available...
                if (dataSockets.ContainsKey(remoteEndPoint))
                {
                    outputSocketInfo = dataSockets[remoteEndPoint];

                    lock (outputSocketInfo.socketLocker)
                    {
                        Socket outSocket = outputSocketInfo.socket;
                        // In this case, the output socket exists for this end point, but it's been disposed (and is still in the collection for some reason)
                        // So we need to recreate and reconnect the socket

                        // This should never happen and has been deprecated
                        //if (outputSocketInfo.socketIsDisposed)
                        //{
                        //    outSocket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);  // Create a new socket in the same slot
                        //    outputSocketInfo.socketIsDisposed = false;
                        //    outSocket.BeginConnect(remoteEndPoint, new AsyncCallback(TCPClientConnect), data);   // Begin async connection to the remote host  
                        //}
                        //// Otherwise the socket exists for this end point and is not disposed
                        //else
                        //{
                        // In this case the output socket exists for this end point, is not disposed, and is connected
                        // So we're good to send the data straight out
                        if (outSocket.IsConnected())
                        {
                            outSocket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(TCPClientSend), new AsyncTCPStateData(outputSocketInfo, data));
                        }
                        // Otherwise the socket exists for this end point, is not disposed, and is not connected
                        // So we need to connect and send
                        else
                        {
                            outSocket.BeginConnect(remoteEndPoint, new AsyncCallback(TCPClientConnect), new AsyncTCPStateData(outputSocketInfo, data));   // Begin async connection to the remote host  
                        }
                        //}
                    }
                }
                // Otherwise we need to create a new socket and connect to the desired remoteEndPoint
                else
                {
                    outputSocketInfo = new SocketInfo(new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp));
                    dataSockets.Add(remoteEndPoint, outputSocketInfo);
                    lock (outputSocketInfo.socketLocker)
                    {
                        outputSocketInfo.socket.BeginConnect(remoteEndPoint, new AsyncCallback(TCPClientConnect), new AsyncTCPStateData(outputSocketInfo, data));   // Begin async connection to the remote host  
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
            // Lock the listener socket to keep it from changing while we're messing with it
            lock (listenerSocketLocker)
            {
                lock (dataSocketsLocker)
                {
                    if (!listenerSocketDisposed)
                    {
                        if (disposing)
                        {
                            if (listenerSocket != null)
                                //socket.Dispose(); Can't do this because of some accessibility goofiness
                                listenerSocket.Close(socketCloseTimeoutMilliseconds);
                        }

                        // Indicate that the instance has been disposed.
                        listenerSocket = null;
                        listenerSocketDisposed = true;
                    }
                    // We can't remove entries from a collection while we're iterating through it, so we'll save the keys to remove for later
                    HashSet<IPEndPoint> keysToRemove = new HashSet<IPEndPoint>();

                    foreach (KeyValuePair<IPEndPoint, SocketInfo> dataSocketEntry in dataSockets)
                    {
                        lock (dataSocketEntry.Value.socketLocker)
                        {
                            if (disposing)
                            {
                                if (dataSockets[dataSocketEntry.Key] != null)
                                {
                                    dataSockets[dataSocketEntry.Key].Dispose();

                                    // Mark this socket entry for removal after we finish interating through the collection
                                    keysToRemove.Add(dataSocketEntry.Key);
                                }
                            }
                        }                        
                    }

                    foreach (IPEndPoint key in keysToRemove)
                    {
                        dataSockets.Remove(key);
                    }
                }
            }
        }

        /// <summary>
        /// <para>Indicates if the comm object's data socket is currently connected.</para>
        /// <para>This is based on active polling of the connection so should be accurate to the actual state of the socket.</para>
        /// </summary>
        public override bool IsConnectedTo(IPEndPoint remoteEndPoint)
        {
            lock (dataSocketsLocker)
            {
                if (dataSockets.ContainsKey(remoteEndPoint))
                {
                    SocketInfo socketInfo = dataSockets[remoteEndPoint];

                    // This timer should always be running if we're actually connected to something
                    lock (socketInfo.checkConnectionTimerLocker)
                    {
                        return socketInfo.checkConnectionTimer != null;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// <para>Checks the status of the comm object's data socket and notifies the parent object's delegate if it's no longer connected.</para> 
        /// </summary>
        /// <param name="state">Null</param>
        private void asyncCheckConnectionTimerElapsed(object state)
        {
            try
            {
                if (!(state is AsyncCheckConnectionStateData))
                {
                    throw new ArgumentException("Argument must be of type AsyncTCPMultiComm.AsyncCheckConnectionStateData", "state");
                }
                AsyncCheckConnectionStateData stateData = (AsyncCheckConnectionStateData)state;
                SocketInfo socketInfo = stateData.socketInfo;
                IPEndPoint remoteEndPoint = stateData.remoteEndPoint;

                lock (socketInfo.socketLocker)
                {
                    // If the data socket's not disposed then check it's status...                    
                    if (socketInfo.socket != null && !socketInfo.IsDisposed)
                    {
                        // Poll the socket to see if it's still connected
                        if (!socketInfo.socket.IsConnected())
                        {
                            // If the socket's not connected any more, notify the constructing object's delegate
                            socketCloseDelegate(SocketError.NotConnected, this, (IPEndPoint)socketInfo.socket.RemoteEndPoint);

                            // We don't want to leave unconnected sockets lying around. We can recreate them as we need them.

                            // Tear down this socket info object
                            socketInfo.Dispose();

                            // Remove it from the dataSockets collection
                            lock (dataSocketsLocker)
                            {
                                if (dataSockets.ContainsKey(remoteEndPoint))
                                {
                                    dataSockets.Remove(remoteEndPoint);
                                }
                            }
                        }
                    }
                    // Otherwise if the data socket's disposed, then we shouldn't be running at all
                    else
                    {
                        socketInfo.Dispose();
                        lock (dataSocketsLocker)
                        {
                            if (dataSockets.ContainsKey(remoteEndPoint))
                            {
                                dataSockets.Remove(remoteEndPoint);
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
        /// Stops the checkConnectionTimer and sets it to null so it will be properly GC'ed.
        /// Thread safe.
        /// </summary>
        private void stopCheckingConnection(SocketInfo socketInfo)
        {
            lock (socketInfo.checkConnectionTimerLocker)
            {
                if (socketInfo.checkConnectionTimer != null)
                {
                    socketInfo.checkConnectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    socketInfo.checkConnectionTimer.Dispose();
                    socketInfo.checkConnectionTimer = null;
                }
            }
        }

        /// <summary>
        /// Convenience method to check if checkConnectionTimer is currently running, i.e. if we think we're supposed to be connected right now.
        /// </summary>
        /// <returns></returns>
        private bool areCheckingConnection(SocketInfo socketInfo)
        {
            lock (socketInfo.checkConnectionTimerLocker)
            {
                return socketInfo.checkConnectionTimer != null;
            }
        }

        /// <summary>
        /// Parses an 1-255.1-255.1-255.1-255:0-65535 end point string from 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private IPEndPoint parseEndPointFromString(string input)
        {
            // Matches 1-255.1-255.1-255.1-255:0-65535. I think.
            const string IPPortRegex = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?):(6553[0-5]|655[0-2]\d|65[0-4]\d\d|6[0-4]\d{3}|[1-5]\d{4}|[1-9]\d{0,3}|0)";

            Match match = Regex.Match(input, IPPortRegex);

            if (match.Success)
            {
                string IPPortString = match.Value;

                string[] ep = IPPortString.Split(':');
                if (ep.Length != 2) throw new FormatException("Invalid endpoint format");
                IPAddress ip;
                if (!IPAddress.TryParse(ep[0], out ip))
                {
                    throw new FormatException("Invalid IP address");
                }
                int port;
                if (!int.TryParse(ep[1], out port))
                {
                    throw new FormatException("Invalid port");
                }
                return new IPEndPoint(ip, port);
            }
            else
            {
                throw new FormatException("Invalid input string, regex could not find an IP:Port string.");
            }
        }

        #endregion

        #region Public properties
        /// <summary>
        /// <para>Indicates if the comm object (specifically, all its sockets) has been disposed.</para>
        /// <para>Synonymous with IsClosed</para>
        /// </summary>
        public override bool IsDisposed
        {
            get
            {
                lock (listenerSocketLocker)
                {
                    lock (dataSocketsLocker)
                    {
                        return ((dataSockets.Count == 0) && listenerSocketDisposed);
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

        public override string ToString()
        {
            StringBuilder outString = new StringBuilder();
            outString.Append("AsyncTCPMultiComm: ");

            lock (dataSocketsLocker)
            {
                foreach (KeyValuePair<IPEndPoint, SocketInfo> kvp in dataSockets)
                {
                    outString.Append(kvp.Value.socket.RemoteEndPoint.ToString() + ", ");
                }
            }

            return outString.ToString();
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
                SocketInfo tempSocketInfo;
                IPEndPoint key;
                // I tried this individually locking each socket as needed, but it gets goofy when you try to dispose the socket,
                // so I just went with taking out both locks for any code section that uses both sockets
                lock (listenerSocketLocker)
                {
                    if (!listenerSocketDisposed)
                    {
                        lock (dataSocketsLocker)
                        {
                            // Store the new socket in a temp variable until we decide what to do with it
                            Socket newlyConnectedSocket = listenerSocket.EndAccept(asyn);

                            // This system will only accept a single connection from each remote end point at a time, so check if the requested connection
                            // is from an end point that we already have a connection to
                            key = (IPEndPoint)newlyConnectedSocket.RemoteEndPoint;
                            if (dataSockets.ContainsKey(key))
                            {
                                // In thes case we already have a connection with the end point that's requesting a new connection
                                // so drop the old connection and accept the new one

                                // Shut down the old connection
                                dataSockets[key].Dispose();
                                // Remove it from the list so it's finalized and GC'ed
                                dataSockets.Remove(key);

                            }
                            // Add the new socket (which is already connected, because this is a connection accept callback)
                            dataSockets.Add(key, new SocketInfo(newlyConnectedSocket));
                            // Make a temporary reference to the newly connected SocketInfo object so we can release the dataSocketsLocker and just lock the SocketInfo components as necessary
                            tempSocketInfo = dataSockets[key];
                        }

                        listenerSocket.BeginAccept(new AsyncCallback(TCPServerConnectionAccept), listenerSocket);   // Begin a new async accept operation
                        byte[] packet = new byte[packetBufferSize];                                             // Create a new packet to store the incoming data

                        // This shouldn't need locking because it's an async operation
                        tempSocketInfo.socket.BeginReceive(packet, 0, (int)packetBufferSize,
                                    SocketFlags.None, new AsyncCallback(TCPDataReceive), new AsyncTCPStateData(tempSocketInfo, packet));             // Start async receive operation, put received data into packet.packetBuffer

                        // Start the connection checker for the new socket
                        lock (tempSocketInfo.checkConnectionTimerLocker)
                        {
                            // Note that this version has to pass the SocketInfo to the callback as the state args so the callback can tell what socket it's supposed to check
                            tempSocketInfo.checkConnectionTimer = new Timer(new TimerCallback(asyncCheckConnectionTimerElapsed), 
                                new AsyncCheckConnectionStateData(tempSocketInfo, key), 0, checkConnectionIntervalMilliseconds);
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
                if (!(asyn.AsyncState is AsyncTCPStateData))
                {
                    throw new ArgumentException("Argument must be of type AsyncTCPStateData", "asyn.AsyncState");
                }
                AsyncTCPStateData stateData = (AsyncTCPStateData)asyn.AsyncState;                                     // Unwrap local packet object from async state wrapper
                byte[] rawBuffer = stateData.packetBuffer;
                SocketInfo socketInfo = stateData.socketInfo;

                // This method can be called when the socket's disposed, for some reason, 
                // so we need to make sure we don't work with a disposed socket
                lock (socketInfo.socketLocker)
                {
                    if (socketInfo.socket != null)
                    {
                        if (socketInfo.socket.IsConnected())                                                       // If this connection's socket is still connected...
                        {
                            // Store the number of bytes received by the socket
                            int receivedDataLength = socketInfo.socket.EndReceive(asyn);
                            if (receivedDataLength == 0)                                                    // If EndReceive returned 0, the remote host has closed the connection                                        
                            {
                                return;                                                                         // Nothing else for this function to do, so return
                            }
                            else
                            {
                                // Packet buffer to process
                                byte[] totalPacketData;
                                // Copy the received bytes and everything remaining in the input buffer into an array to pass to the delegate
                                lock (socketInfo.inputBuffer)
                                {
                                    totalPacketData = new byte[receivedDataLength + socketInfo.inputBuffer.Count];
                                    // Copy anything lingering in the input buffer into the packet buffer to process
                                    Array.Copy(socketInfo.inputBuffer.ToArray(), totalPacketData, socketInfo.inputBuffer.Count);
                                    // Copy the just received data into the end of the packet buffer to process as well
                                    Array.Copy(rawBuffer, 0, totalPacketData, socketInfo.inputBuffer.Count, receivedDataLength);
                                }

                                if (commMode == CommMode.Packet)
                                {
                                    // Validate the packet using the delegate we were passed at construction
                                    ValidatePacketResponse validateResponse = validatePacketDelegate(totalPacketData, (uint)receivedDataLength, this, (IPEndPoint)socketInfo.socket.RemoteEndPoint);
                                    // If the delegate flagged the packet as valid...
                                    if (validateResponse.packetIsValid)
                                    {
                                        // Copy just the packet data into an output buffer
                                        byte[] returnData = new byte[validateResponse.packetLength];
                                        Array.Copy(totalPacketData, validateResponse.packetStartIndex, returnData, 0, validateResponse.packetLength);
                                        // Pass the validated packet data back using a delegate
                                        receievedPacketDelegate(returnData, this, (IPEndPoint)socketInfo.socket.RemoteEndPoint);

                                        // If we've seen a valid packet, anything remaining in the input buffer can be flushed
                                        lock (socketInfo.inputBuffer)
                                        {
                                            socketInfo.inputBuffer.Clear();
                                        }

                                        // If the validated packet does not extend to the end of the packet data, 
                                        // we need to copy the extra into the input buffer for later use
                                        uint validatedEndIndex = validateResponse.packetStartIndex + validateResponse.packetLength;

                                        if (validatedEndIndex < totalPacketData.Length)
                                        {
                                            byte[] leftovers = new byte[totalPacketData.Length - validatedEndIndex];
                                            Array.Copy(totalPacketData, validatedEndIndex, leftovers, 0, totalPacketData.Length - validatedEndIndex);
                                            lock (socketInfo.inputBuffer)
                                            {
                                                socketInfo.inputBuffer.AddRange(leftovers);
                                            }
                                        }
                                    }
                                    // Otherwise the delegate rejected the packet, so we need to hang onto the new packet data in case a piece of it is needed later.
                                    else
                                    {
                                        lock (socketInfo.inputBuffer)
                                        {
                                            byte[] rawReceivedData = new byte[receivedDataLength];
                                            Array.Copy(rawBuffer, rawReceivedData, receivedDataLength);

                                            socketInfo.inputBuffer.AddRange(rawReceivedData);
                                        }
                                    }
                                }
                                else if (commMode == CommMode.Stream)
                                {
                                    lock (socketInfo.inputBuffer)
                                    {
                                        // Add the received data to the total input stream data
                                        socketInfo.inputBuffer.AddRange(totalPacketData);
                                        // Pass a reference to the input stream data to the delegate we were passed at construction
                                        receivedStreamDataDelegate(socketInfo.inputBuffer, this, (IPEndPoint)socketInfo.socket.RemoteEndPoint);
                                    }
                                }

                                if (socketInfo.socket.IsConnected())                                                       // If the socket's still connected on this end...
                                {
                                    byte[] responsePacket = new byte[packetBufferSize];
                                    socketInfo.socket.BeginReceive(responsePacket, 0, (int)packetBufferSize,               // Call this function again to continue receiving data from the remote host
                                        SocketFlags.None, new AsyncCallback(TCPDataReceive), new AsyncTCPStateData(socketInfo, responsePacket));
                                }
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
        /// Client connection callback. Creates a connection to a remote host.
        /// </summary>
        /// <param name="asyn">Asynch state object. The byte buffer of data to send.</param>
        /// <returns>Nothing.</returns>
        private void TCPClientConnect(IAsyncResult asyn)
        {
            try
            {
                if (!(asyn.AsyncState is AsyncTCPStateData))
                {
                    throw new ArgumentException("Argument must be of type AsyncTCPStateData", "asyn.AsyncState");
                }
                AsyncTCPStateData stateData = (AsyncTCPStateData)asyn.AsyncState;                                     // Unwrap local packet object from async state wrapper
                byte[] packetOut = stateData.packetBuffer;
                SocketInfo socketInfo = stateData.socketInfo;

                lock (socketInfo.socketLocker)
                {
                    socketInfo.socket.EndConnect(asyn);                                                    // Finish the async connect operation

                    // Start periodically checking this connection to see if it's still connected
                    lock (socketInfo.checkConnectionTimerLocker)
                    {
                        socketInfo.checkConnectionTimer = new Timer(new TimerCallback(asyncCheckConnectionTimerElapsed), 
                            new AsyncCheckConnectionStateData(socketInfo, (IPEndPoint)socketInfo.socket.RemoteEndPoint), 0, checkConnectionIntervalMilliseconds);
                    }

                    byte[] packetRemoteResponse = new byte[packetBufferSize];                       // Create a new packet object to hold the remote host's response
                    socketInfo.socket.BeginReceive(packetRemoteResponse, 0, (int)packetBufferSize,         // Call this function again to continue receiving data from the remote host
                        SocketFlags.None, new AsyncCallback(TCPDataReceive), new AsyncTCPStateData(socketInfo, packetRemoteResponse));
                    socketInfo.socket.BeginSend(packetOut, 0, packetOut.Length,                            // Begin async data transmit of the original packet
                        SocketFlags.None, new AsyncCallback(TCPClientSend), new AsyncTCPStateData(socketInfo, packetOut));
                }
            }
            catch (SocketException e)
            {
                IPEndPoint endPoint;
                // The clowns who made these socket exceptions apparently didn't see fit to include the socket or remote end point that threw the
                // error in the action SocketException object, so we have to parse it out of Exception.Message
                endPoint = parseEndPointFromString(e.Message);

                switch (e.SocketErrorCode)
                {
                    // Connection actively refused
                    case SocketError.ConnectionRefused:
                        // this shouldn't happen
                        if (endPoint == null)
                        {
                            socketCloseDelegate(SocketError.ConnectionRefused, this, anyEndPoint);
                            // If this is the case, we can't remove the socket from dataSockets because we don't know it's end point
                            // So we'll end up with an inactive socket in dataSockets that will be reset if it's end point is ever used
                            // which I think is fine.
                        }
                        // This should
                        else
                        {
                            socketCloseDelegate(SocketError.ConnectionRefused, this, endPoint);
                            lock (dataSocketsLocker)
                            {
                                dataSockets[endPoint].Dispose();
                                dataSockets.Remove(endPoint);
                            }
                        }
                        break;
                    // Connection reset
                    case SocketError.ConnectionReset:
                        //
                        if (endPoint == null)
                        {
                            socketCloseDelegate(SocketError.ConnectionReset, this, anyEndPoint);
                        }
                        else
                        {
                            socketCloseDelegate(SocketError.ConnectionReset, this, endPoint);
                            lock (dataSocketsLocker)
                            {
                                dataSockets[endPoint].Dispose();
                                dataSockets.Remove(endPoint);
                            }
                        }
                        break;
                    // Who knows
                    default:
                        commExceptionDelegate(e, this);
                        if (endPoint != null)
                        {
                            lock (dataSocketsLocker)
                            {
                                dataSockets[endPoint].Dispose();
                                dataSockets.Remove(endPoint);
                            }
                        }
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
                if (!(asyn.AsyncState is AsyncTCPStateData))
                {
                    throw new ArgumentException("Argument must be of type AsyncTCPStateData", "asyn.AsyncState");
                }
                AsyncTCPStateData stateData = (AsyncTCPStateData)asyn.AsyncState;                                     // Unwrap local packet object from async state wrapper
                byte[] packetOut = stateData.packetBuffer;
                SocketInfo socketInfo = stateData.socketInfo;

                lock (socketInfo.socketLocker)
                {
                    int bytesSent = 0;                                                              // Holds the number of bytes output by this send operation
                    // This should probably be used to call this Send operation again if the bytes out is less than the length of the send buffer, but I haven't tested
                    // if the TCP stack handles fragmentation itself or not yet

                    bytesSent = socketInfo.socket.EndSend(asyn);                                       // Save the number of bytes sent in this send operation
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
