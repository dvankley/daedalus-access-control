using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace NetworkHelpers
{
    public abstract class AsyncComm
    {
        //http://msdn.microsoft.com/en-us/library/system.net.sockets.socket.listen.aspx I don't understand what this is or does
        protected const int pendingServerConnectionBacklog = 10;
        protected const int checkConnectionIntervalMilliseconds = 1000;
        protected const int socketCloseTimeoutMilliseconds = 2;
        protected readonly IPEndPoint anyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        public enum CommMode { Stream, Packet };

        public struct ValidatePacketResponse
        {
            public bool packetIsValid;
            public uint packetStartIndex;
            public uint packetLength;

            public ValidatePacketResponse(bool packetIsValid, uint packetStartIndex, uint packetLength)
            {
                this.packetIsValid = packetIsValid;
                this.packetStartIndex = packetStartIndex;
                this.packetLength = packetLength;
            }
        }

        /// <summary>
        /// <para>Delegate to verify that a given byte array is a valid packet for this particular application.</para>
        /// <para>Note that all these delegates are called on separate thread pool threads, so handlers must be thread safe.</para>
        /// </summary>
        /// <param name="packetData">Reference to the current input byte data to be checked for a valid packet.</param>
        /// <param name="maxLength">The maximum length of the input data in <paramref name="packetData"/></param>
        /// <param name="comm">A reference to the AsyncComm object that called this delegate</param>
        /// <returns><para>A ValidatePacketResponse structure.</para> 
        /// <para>Returning ValidatePacketResponse.isValidPacket = true will cause the ReceivedPacketDelegate to be called with the data pointed to by the ValidatePacketResponse struct.</para>
        /// <para>Returning ValidatePacketResponse.isValidPacket = false will cause the AsyncComm object to store this packet in the input buffer and wait for further input to call this method again.</para></returns>
        public delegate ValidatePacketResponse ValidatePacketDelegate(byte[] packetData, uint maxLength, AsyncComm comm, IPEndPoint remoteEndPoint);

        /// <summary>
        /// <para>Delegate called to pass validated packet data back the parent class after data is received and verified via ValidatePacketDelegate.</para>
        /// <para>Note that all these delegates are called on separate thread pool threads, so handlers must be thread safe.</para>
        /// </summary>
        /// <param name="packetData">A buffer containing only the validated packet data.</param>
        /// <param name="comm">A reference to the AsyncComm object that called this delegate.</param>
        public delegate void ReceivedPacketDelegate(byte[] packetData, AsyncComm comm, IPEndPoint remoteEndPoint);

        /// <summary>
        /// <para>Delegate called to indicate to the parent class that the AsyncTCPComm object's dataSocket has closed.</para>
        /// <para>Note that all these delegates are called on separate thread pool threads, so handlers must be thread safe.</para>
        /// </summary>
        /// <param name="socketEvent">Enum used to indicate what event took place to cause this delegate to be called.</param>
        /// <param name="comm">A reference to the AsyncTCPComm object that called this delegate.</param>
        public delegate void SocketCloseDelegate(SocketError socketEvent, AsyncComm comm, IPEndPoint remoteEndPoint);

        /// <summary>
        /// <para>Delegate called to pass the entire input data stream back the parent class after data is received.</para>
        /// <para>Note that all these delegates are called on separate thread pool threads, so handlers must be thread safe.</para>
        /// </summary>
        /// <param name="packetData"><para>A reference to the AsyncTCPComm object's current input buffer.</para>
        /// <para>Note that this delegate is responsible for clearing this buffer of unneeded data as necessary.</para></param>
        /// <param name="comm">A reference to the AsyncComm object that called this delegate.</param>
        public delegate void ReceivedStreamDataDelegate(List<byte> packetData, AsyncComm comm, IPEndPoint remoteEndPoint);

        /// <summary>
        /// <para>Delegate called to indicate to the parent class that an exception has taken place in the AsyncTCPComm object.</para>
        /// <para>Note that this will only be called for exceptions thrown in async methods. Methods called directly by the parent class will throw exceptions up the chain as normal.</para>
        /// <para>Note that all these delegates are called on separate thread pool threads, so handlers must be thread safe.</para>
        /// </summary>
        /// <param name="exception">Exception thrown.</param>
        /// <param name="comm">A reference to the AsyncComm object that called this delegate.</param>
        public delegate void CommExceptionDelegate(Exception exception, AsyncComm comm);

        /// <summary>
        /// <para>Begins listening on the specified local end point for remote connections (if TCP) or data (if UDP).</para>
        /// <para>If AsyncSingleComm, the class only supports one data connection at a time. Further connection requests will drop the first.</para>
        /// <para>If AsyncMultiComm, the class supports an arbitrary number of connections, although is limited to only one connection per remote end point. 
        /// Further connection requests on the same remote end point will drop the previous connection.</para>
        /// </summary>
        /// <param name="localEndPoint">The local end point (IP address and port) to listen for connections on.</param>
        /// <returns>True if successful, false otherwise.</returns>
        /// <exception cref="AsyncTCPCommException">Thrown to indicate a general exception with the AsyncTCPComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public abstract bool Listen(IPEndPoint localEndPoint);

        /// <summary>
        /// <para>Starts an async operation to send <paramref name="data"/> to <paramref name="remoteEndPoint"/></para>
        /// <para>If TCP, will connect to the end point if a connection does not already exist.</para>
        /// <para>If a connection to the specified end point already exists, whether from a previous send operation or from an incoming connection, the data will be sent on that connection.</para>
        /// <para>If AsyncSingleComm and a previous connection to anywhere but the specified end point already exists, it will be dropped and a connection to the specified end point initiated.</para>
        /// </summary>
        /// <param name="remoteEndPoint">The end point to send <paramref name="data"/> to.</param>
        /// <param name="data">The data buffer to send to <paramref name="remoteEndPoint"/></param>
        /// <returns>True if successful, false otherwise.</returns>
        /// <exception cref="AsyncTCPCommException">Thrown to indicate a general exception with the AsyncTCPComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public abstract bool Send(IPEndPoint remoteEndPoint, byte[] data);

        /// <summary>
        /// Closes and disposes of all resources of this comm object. IsClosed and IsDisposed are sent when finished. Synonymous with Dispose().
        /// </summary>
        /// <exception cref="AsyncTCPCommException">Thrown to indicate a general exception with the AsyncComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public abstract void Close();

        /// <summary>
        /// Closes and disposes of all resources of this comm object. IsClosed and IsDisposed are sent when finished. Synonymous with Close().
        /// </summary>
        /// <exception cref="AsyncTCPCommException">Thrown to indicate a general exception with the AsyncComm class.</exception>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        public abstract void Dispose();

        /// <summary>
        /// <para>Indicates if the comm object (specifically, both its sockets) has been disposed.</para>
        /// <para>Synonymous with IsClosed</para>
        /// </summary>
        public abstract bool IsDisposed { get; }

        /// <summary>
        /// <para>Indicates if the comm object (specifically, both its sockets) has been disposed.</para>
        /// <para>Synonymous with IsDisposed</para>
        /// </summary>
        public abstract bool IsClosed { get; }

        public override abstract string ToString();        
    }

    public abstract class AsyncSingleComm : AsyncComm
    {
        /// <summary>
        /// <para>If TCP, the remote end point for this comm object's dataSocket. Null if not connected or instantiated.</para>
        /// <para>If UDP, the last remote end point data was sent to or received from.</para>
        /// </summary>
        public abstract IPEndPoint remoteEndPoint { get; }

        /// <summary>
        /// <para>Indicates if the comm object's data socket is currently connected.</para>
        /// <para>This is based on active polling of the connection so should be accurate to the actual state of the socket.</para>
        /// <para>If UDP, this always returns true to support interoperability with TCP code.</para>
        /// </summary>
        public abstract bool IsConnected { get; }
    }

    public abstract class AsyncMultiComm : AsyncComm
    {
        /// <summary>
        /// <para>Indicates if any one of the comm object's data sockets is currently connected to <paramref name="remoteEndPoint"/>.</para>
        /// <para>This is based on active polling of the connection so should be accurate to the actual state of the socket.</para>
        /// <param name="remoteEndPoint">End point to check connection to.</param>
        /// </summary>
        public abstract bool IsConnectedTo(IPEndPoint remoteEndPoint);
    }

    #region Exceptions
    [Serializable()]
    public class AsyncCommException : System.ApplicationException
    {
        public AsyncCommException() : base() { }
        public AsyncCommException(string message) : base(message) { }
        public AsyncCommException(string message, System.Exception inner) : base(message, inner) { }


        public Exception innerException
        {
            get
            {
                return base.InnerException;
            }
        }

        // Constructor needed for serialization 
        // when exception propagates from a remoting server to the client. 
        protected AsyncCommException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) { }
    }
    #endregion
}
