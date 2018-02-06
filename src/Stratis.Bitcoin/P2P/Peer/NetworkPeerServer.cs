using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; private set; }

        /// <summary>Version of the protocol that the server is running.</summary>
        public ProtocolVersion Version { get; private set; }

        /// <summary>The parameters that will be cloned and applied for each peer connecting to <see cref="NetworkPeerServer"/>.</summary>
        public NetworkPeerConnectionParameters InboundNetworkPeerConnectionParameters { get; set; }

        /// <summary>Maximum number of inbound connection that the server is willing to handle simultaneously.</summary>
        public const int MaxConnectionThreshold = 125;

        /// <summary>
        /// The amount of time in seconds we should stall before checking if we can accept incoming connections again.
        /// This happens once we have reached the maximum connection threshold.
        /// </summary>
        public const int MaxConnectionThresholdStallTime = 60;

        /// <summary>IP address and port, on which the server listens to incoming connections.</summary>
        public IPEndPoint LocalEndpoint { get; private set; }

        /// <summary>IP address and port of the external network interface that is accessible from the Internet.</summary>
        public IPEndPoint ExternalEndpoint { get; private set; }

        /// <summary>TCP server listener accepting inbound connections.</summary>
        private TcpListener tcpListener;

        /// <summary>List of network client peers that are currently connected to the server.</summary>
        public NetworkPeerCollection ConnectedNetworkPeers { get; private set; }

        /// <summary>Cancellation that is triggered on shutdown to stop all pending operations.</summary>
        private readonly CancellationTokenSource serverCancel;

        /// <summary>List of active clients' connections mapped by their unique identifiers.</summary>
        private readonly ConcurrentDictionary<int, NetworkPeerConnection> connectionsById;

        /// <summary>Task accepting new clients in a loop.</summary>
        private Task acceptTask;

        /// <summary>
        /// Initializes instance of a network peer server.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="localEndPoint">IP address and port to listen on.</param>
        /// <param name="externalEndPoint">IP address and port that the server is reachable from the Internet on.</param>
        /// <param name="version">Version of the network protocol that the server should run.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="networkPeerFactory">Factory for creating P2P network peers.</param>
        public NetworkPeerServer(Network network, IPEndPoint localEndpoint, IPEndPoint externalEndpoint, ProtocolVersion version, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INetworkPeerFactory networkPeerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{localEndpoint}] ");
            this.logger.LogTrace("({0}:{1},{2}:{3},{4}:{5})", nameof(network), network, nameof(localEndpoint), localEndpoint, nameof(externalEndpoint), externalEndpoint, nameof(version), version);

            this.dateTimeProvider = dateTimeProvider;
            this.networkPeerFactory = networkPeerFactory;

            this.InboundNetworkPeerConnectionParameters = new NetworkPeerConnectionParameters();

            this.LocalEndpoint = Utils.EnsureIPv6(localEndpoint);
            this.ExternalEndpoint = Utils.EnsureIPv6(externalEndpoint);

            this.Network = network;
            this.Version = version;

            this.ConnectedNetworkPeers = new NetworkPeerCollection();

            this.serverCancel = new CancellationTokenSource();

            this.tcpListener = new TcpListener(this.LocalEndpoint);
            this.tcpListener.Server.LingerState = new LingerOption(true, 0);
            this.tcpListener.Server.NoDelay = true;
            this.tcpListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

            this.connectionsById = new ConcurrentDictionary<int, NetworkPeerConnection>();
            this.acceptTask = Task.CompletedTask;

            this.logger.LogTrace("Network peer server ready to listen on '{0}'.", this.LocalEndpoint);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Starts listening on the server's initialialized endpoint.
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
                            Task<TcpClient> acceptTask = this.tcpListener.AcceptTcpClientAsync();
                            acceptTask.Wait(this.serverCancel.Token);
                            return acceptTask.Result;
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

                    if (this.ConnectedNetworkPeers.Count >= MaxConnectionThreshold)
                    {
                        this.logger.LogTrace("Maximum connection threshold [{0}] reached, stall accepting of incoming connections.", MaxConnectionThreshold);
                        tcpClient.Close();

                        await Task.Delay(MaxConnectionThresholdStallTime, this.serverCancel.Token).ConfigureAwait(false);
                        continue;
                    }

                    this.logger.LogTrace("Connection accepted from client '{0}'.", tcpClient.Client.RemoteEndPoint);

                    NetworkPeer networkPeer = this.networkPeerFactory.CreateNetworkPeer(this.Network, tcpClient, this.CreateNetworkPeerConnectionParameters());

                    this.ConnectedNetworkPeers.Add(networkPeer);
                    networkPeer.StateChanged.Register(this.OnStateChangedAsync);

                    this.AddClientConnection(networkPeer.Connection);
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogDebug("Shutdown detected, stop accepting connections.");
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Adds client's connection to the list of active connections.
        /// </summary>
        /// <param name="connection">Client's connection to add.</param>
        private void AddClientConnection(NetworkPeerConnection connection)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(connection), nameof(connection.Id), connection.Id);

            this.connectionsById.AddOrReplace(connection.Id, connection);
            connection.DisposeComplete.Task.ContinueWith(unused => this.RemoveConnectedClient(connection));

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Removes a connection from the list of active clients' connection.
        /// </summary>
        /// <param name="connection">Client to remove and disconnect.</param>
        private void RemoveConnectedClient(NetworkPeerConnection connection)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(connection), nameof(connection.Id), connection.Id);

            if (!this.connectionsById.TryRemove(connection.Id, out NetworkPeerConnection unused))
                this.logger.LogError("Internal data integration error.");

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Callback that is called when a network status of a connected client peer changes.
        /// <para>The peer is removed from the list of connected peers if the connection has been terminated for any reason.</para>
        /// </summary>
        /// <param name="peer">The connected peer.</param>
        /// <param name="oldState">Previous state of the peer. New state of the peer is stored in its <see cref="NetworkPeer.State"/> property.</param>
        private Task OnStateChangedAsync(NetworkPeer peer, NetworkPeerState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}.{5}:{6})", nameof(peer), peer.PeerEndPoint, nameof(oldState), oldState, nameof(peer), nameof(peer.State), peer.State);

            if ((peer.State == NetworkPeerState.Disconnecting)
                || (peer.State == NetworkPeerState.Failed)
                || (peer.State == NetworkPeerState.Offline))
            {
                peer.StateChanged.Unregister(this.OnStateChangedAsync);
                this.ConnectedNetworkPeers.Remove(peer, "Peer disconnected");
            }

            this.logger.LogTrace("(-)");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.serverCancel.Cancel();

            this.logger.LogTrace("Stopping network peer server.");

            try
            {
                this.ConnectedNetworkPeers.DisconnectAll("Node shutdown");
            }
            catch (Exception e)
            {
                this.logger.LogTrace("Exception occurred: {0}", e.ToString());
            }

            this.logger.LogTrace("Stopping TCP listener.");
            this.tcpListener?.Stop();

            this.logger.LogTrace("Waiting for accepting task to complete.");
            this.acceptTask.Wait();

            ICollection<NetworkPeerConnection> connections = this.connectionsById.Values;
            if (connections.Count > 0)
            {
                this.logger.LogInformation("Waiting for {0} connected clients to finish.", connections.Count);
                foreach (NetworkPeerConnection connection in connections)
                {
                    this.logger.LogTrace("Disposing and waiting for connection ID {0}.", connection.Id);
                    TaskCompletionSource<bool> completion = connection.DisposeComplete;
                    connection.Dispose();
                    completion.Task.Wait();
                }
            }

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