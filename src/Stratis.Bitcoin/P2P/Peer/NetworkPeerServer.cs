﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    public class NetworkPeerServer : IDisposable
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; private set; }

        /// <summary>Version of the protocol that the server is running.</summary>
        public ProtocolVersion Version { get; private set; }

        /// <summary>The parameters that will be cloned and applied for each peer connecting to <see cref="NetworkPeerServer"/>.</summary>
        public NetworkPeerConnectionParameters InboundNetworkPeerConnectionParameters { get; set; }

        /// <summary>Maximum number of inbound connection that the server is willing to handle simultaneously.</summary>
        /// <remarks>TODO: consider making this configurable.</remarks>
        public const int MaxConnectionThreshold = 125;

        /// <summary>IP address and port, on which the server listens to incoming connections.</summary>
        public IPEndPoint LocalEndpoint { get; private set; }

        /// <summary>IP address and port of the external network interface that is accessible from the Internet.</summary>
        public IPEndPoint ExternalEndpoint { get; private set; }

        /// <summary>TCP server listener accepting inbound connections.</summary>
        private readonly TcpListener tcpListener;

        /// <summary>Cancellation that is triggered on shutdown to stop all pending operations.</summary>
        private readonly CancellationTokenSource serverCancel;

        /// <summary>Maintains a list of connected peers and ensures their proper disposal.</summary>
        private readonly NetworkPeerDisposer networkPeerDisposer;

        /// <summary>Task accepting new clients in a loop.</summary>
        private Task acceptTask;

        /// <summary>
        /// Initializes instance of a network peer server.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="localEndPoint">IP address and port to listen on.</param>
        /// <param name="externalEndPoint">IP address and port that the server is reachable from the Internet on.</param>
        /// <param name="version">Version of the network protocol that the server should run.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="networkPeerFactory">Factory for creating P2P network peers.</param>
        public NetworkPeerServer(Network network,
            IPEndPoint localEndPoint,
            IPEndPoint externalEndPoint,
            ProtocolVersion version,
            ILoggerFactory loggerFactory,
            INetworkPeerFactory networkPeerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{localEndPoint}] ");
            this.logger.LogTrace("({0}:{1},{2}:{3},{4}:{5})", nameof(network), network, nameof(localEndPoint), localEndPoint, nameof(externalEndPoint), externalEndPoint, nameof(version), version);

            this.networkPeerFactory = networkPeerFactory;
            this.networkPeerDisposer = new NetworkPeerDisposer(loggerFactory);

            this.InboundNetworkPeerConnectionParameters = new NetworkPeerConnectionParameters();

            this.LocalEndpoint = Utils.EnsureIPv6(localEndPoint);
            this.ExternalEndpoint = Utils.EnsureIPv6(externalEndPoint);

            this.Network = network;
            this.Version = version;

            this.serverCancel = new CancellationTokenSource();

            this.tcpListener = new TcpListener(this.LocalEndpoint);
            this.tcpListener.Server.LingerState = new LingerOption(true, 0);
            this.tcpListener.Server.NoDelay = true;
            this.tcpListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

            this.acceptTask = Task.CompletedTask;

            this.logger.LogTrace("Network peer server ready to listen on '{0}'.", this.LocalEndpoint);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Starts listening on the server's initialized endpoint.
        /// </summary>
        public void Listen()
        {
            this.logger.LogTrace("()");

            try
            {
                this.tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                this.tcpListener.Start();
                this.acceptTask = this.AcceptClientsAsync();
            }
            catch (Exception e)
            {
                this.logger.LogTrace("Exception occurred: {0}", e.ToString());
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Implements loop accepting connections from newly connected clients.
        /// </summary>
        private async Task AcceptClientsAsync()
        {
            this.logger.LogTrace("()");

            this.logger.LogTrace("Accepting incoming connections.");

            try
            {
                while (!this.serverCancel.IsCancellationRequested)
                {
                    // Used to record any errors occurring in the thread pool task.
                    Exception error = null;

                    TcpClient tcpClient = await Task.Run(() =>
                    {
                        try
                        {
                            Task<TcpClient> acceptClientTask = this.tcpListener.AcceptTcpClientAsync();
                            acceptClientTask.Wait(this.serverCancel.Token);
                            return acceptClientTask.Result;
                        }
                        catch (Exception exception)
                        {
                            // Record the error.
                            error = exception;
                            return null;
                        }
                    }).ConfigureAwait(false);

                    // Raise the error.
                    if (error != null)
                        throw error;

                    if (this.networkPeerDisposer.ConnectedPeersCount >= MaxConnectionThreshold)
                    {
                        this.logger.LogTrace("Maximum connection threshold [{0}] reached, closing the client.", MaxConnectionThreshold);
                        tcpClient.Close();
                        continue;
                    }

                    this.logger.LogTrace("Connection accepted from client '{0}'.", tcpClient.Client.RemoteEndPoint);

                    this.networkPeerFactory.CreateNetworkPeer(tcpClient, this.CreateNetworkPeerConnectionParameters(), this.networkPeerDisposer);
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogDebug("Shutdown detected, stop accepting connections.");
            }
            catch (Exception e)
            {
                this.logger.LogDebug("Exception occurred: {0}", e.ToString());
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.serverCancel.Cancel();

            this.logger.LogTrace("Stopping TCP listener.");
            this.tcpListener.Stop();

            this.logger.LogTrace("Waiting for accepting task to complete.");
            this.acceptTask.Wait();

            if (this.networkPeerDisposer.ConnectedPeersCount > 0)
                this.logger.LogInformation("Waiting for {0} connected clients to finish.", this.networkPeerDisposer.ConnectedPeersCount);

            this.networkPeerDisposer.Dispose();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Initializes connection parameters using the server's initialized values.
        /// </summary>
        /// <returns>Initialized connection parameters.</returns>
        private NetworkPeerConnectionParameters CreateNetworkPeerConnectionParameters()
        {
            IPEndPoint myExternal = this.ExternalEndpoint;
            NetworkPeerConnectionParameters param2 = this.InboundNetworkPeerConnectionParameters.Clone();
            param2.Version = this.Version;
            param2.AddressFrom = myExternal;
            return param2;
        }
    }
}