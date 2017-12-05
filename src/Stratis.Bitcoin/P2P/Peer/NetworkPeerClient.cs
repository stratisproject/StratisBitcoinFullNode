using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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

        /// <summary>Prevents parallel execution of multiple write operations on <see cref="Stream"/>.</summary>
        private AsyncLock writeLock;

        /// <summary>Stream to send and receive messages through established TCP connection.</summary>
        /// <remarks>Write operations on the stream have to be protected by <see cref="writeLock"/>.</remarks>
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

            this.Stream = this.tcpClient.GetStream();
            this.ProcessingCompletion = new TaskCompletionSource<bool>();

            this.writeLock = new AsyncLock();
        }

        /// <summary>
        /// Connects the network client to the target server.
        /// </summary>
        /// <param name="endPoint">IP address and port to connect to.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the connection attempt was aborted.</exception>
        public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken cancellation)
        {
            this.logger = this.loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.Id}-{endPoint}] ");
            this.logger.LogTrace("({0}:'{1}')", nameof(endPoint), endPoint);

            try
            {
                await Task.Run(async () => await this.tcpClient.ConnectAsync(endPoint.Address, endPoint.Port), cancellation);
                this.Stream = this.tcpClient.GetStream();
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException)
                {
                    this.logger.LogTrace("Connecting to '{0}' cancelled.", endPoint);
                    this.logger.LogTrace("(-)[CANCELLED]");
                }
                else
                {
                    this.logger.LogDebug("Error connecting to '{0}', exception: {1}", endPoint, e.ToString());
                    this.logger.LogTrace("(-)[UNHANDLED_EXCEPTION]");
                }
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sends data over the established connection.
        /// </summary>
        /// <param name="data">Data to send.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the connection was terminated or the cancellation token was cancelled.</exception>
        public async Task SendAsync(byte[] data, CancellationToken cancellation)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(data), nameof(data.Length), data.Length);

            using (await this.writeLock.LockAsync(cancellation))
            {
                if (this.Stream == null)
                {
                    this.logger.LogTrace("Connection has been terminated.");
                    this.logger.LogTrace("(-)[NO_STREAM]");
                    throw new OperationCanceledException();
                }

                try
                {
                    await this.Stream.WriteAsync(data, 0, data.Length, cancellation);
                }
                catch (Exception e)
                {
                    if ((e is IOException) || (e is OperationCanceledException))
                    {
                        this.logger.LogTrace("Connection has been terminated.");
                        if (e is IOException) this.logger.LogTrace("(-)[IO_EXCEPTION]");
                        else this.logger.LogTrace("(-)[CANCELLED]");
                        throw new OperationCanceledException();
                    }
                    else
                    {
                        this.logger.LogTrace("Exception occurred: {0}", e.ToString());
                        this.logger.LogTrace("(-)[UNHANDLED_EXCEPTION]");
                        throw;
                    }
                }
            }

            this.logger.LogTrace("(-)");
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
