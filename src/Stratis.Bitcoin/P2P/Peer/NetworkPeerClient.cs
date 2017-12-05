using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>
    /// Represents a TCP client that can connect to a server and send and receive messages.
    /// </summary>
    public class NetworkPeerClient : IDisposable
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private ILogger logger;

        /// <summary>Unique identifier of a client.</summary>
        public int Id { get; private set; }

        /// <summary>Underlaying TCP client.</summary>
        private TcpClient tcpClient;

        /// <summary>Stream to send and receive messages through established TCP connection.</summary>
        public NetworkStream Stream { get; private set; }

        /// <summary>Task that cares about the client once its connection is accepted up until the communication is terminated.</summary>
        /// <remarks>The client is being processed as long as this task is not complete.</remarks>
        public TaskCompletionSource<bool> ProcessingCompletion { get; private set; }

        /// <summary>Address of the end point the client is connected to, or <c>null</c> if the client has not connected yet.</summary>
        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return (IPEndPoint)this.tcpClient?.Client?.RemoteEndPoint;
            }
        }

        /// <summary>
        /// Initializes a new network client.
        /// </summary>
        /// <param name="Id">Unique identifier of a client.</param>
        /// <param name="tcpClient">Initialized TCP client, which may or may not be already connected.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public NetworkPeerClient(int Id, TcpClient tcpClient, ILoggerFactory loggerFactory)
        {
            this.tcpClient = tcpClient;

            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, string.Format("[{0}{1}] ", this.Id, this.RemoteEndPoint != null ? "-" + this.RemoteEndPoint.ToString() : ""));

            this.Stream = tcpClient.GetStream();
            this.ProcessingCompletion = new TaskCompletionSource<bool>();
        }

        /// <summary>
        /// Connects the network client to the target server.
        /// </summary>
        /// <param name="endPoint">IP address and port to connect to.</param>
        /// <returns><c>true</c> if the connection has been established, <c>false</c> otherwise.</returns>
        public async Task<bool> ConnectAsync(IPEndPoint endPoint)
        {
            this.logger = this.loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.Id}-{endPoint}] ");
            this.logger.LogTrace("({0}:'{1}')", nameof(endPoint), endPoint);

            bool res = false;
            try
            {
                await this.tcpClient.ConnectAsync(endPoint.Address, endPoint.Port);
                this.Stream = this.tcpClient.GetStream();
                res = true;
            }
            catch (Exception e)
            {
                this.logger.LogDebug("Error connecting to '{0}', exception: {1}", endPoint, e.ToString());
            }

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            NetworkStream disposeStream = this.Stream;
            TcpClient disposeTcpClient = this.tcpClient;

            this.Stream = null;
            this.tcpClient = null;

            disposeStream?.Dispose();
            disposeTcpClient?.Dispose();

            this.logger.LogTrace("(-)");
        }
    }
}
