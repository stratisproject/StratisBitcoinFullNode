using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
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

        /// <summary><c>true</c> to allow connections from LAN, <c>false</c> otherwise.</summary>
        public bool AllowLocalPeers { get; set; }

        /// <summary>Maximal number of inbound connection that the server is willing to handle simultaneously.</summary>
        public int MaxConnections { get; set; }

        /// <summary>IP address and port, on which the server listens to incoming connections.</summary>
        public IPEndPoint LocalEndpoint { get; private set; }

        /// <summary>IP address and port of the external network interface that is accessible from the Internet.</summary>
        public IPEndPoint ExternalEndpoint { get; private set; }

        /// <summary>TCP server listener accepting inbound connections.</summary>
        private TcpListener tcpListener;

        /// <summary>Queue of incoming messages distributed to message consumers.</summary>
        private readonly MessageProducer<IncomingMessage> messageProducer = new MessageProducer<IncomingMessage>();

        /// <summary>List of network client peers that are currently connected to the server.</summary>
        public NetworkPeerCollection ConnectedNetworkPeers { get; private set; }

        /// <summary>Cancellation that is triggered on shutdown to stop all pending operations.</summary>
        private readonly CancellationTokenSource serverCancel;

        /// <summary>Number of unfinished tasks that we need to wait for before we consider shutdown of the object completed.</summary>
        private volatile int unfinishedTasks;

        /// <summary>Nonce for server's version payload.</summary>
        private ulong nonce;
        /// <summary>Nonce for server's version payload.</summary>
        public ulong Nonce
        {
            get
            {
                if (this.nonce == 0)
                    this.nonce = RandomUtils.GetUInt64();

                return this.nonce;
            }
            set
            {
                this.nonce = value;
            }
        }

        /// <summary>Consumer of messages coming from connected clients.</summary>
        /// <seealso cref="ProcessMessage(IncomingMessage)"/>
        private readonly EventLoopMessageListener<IncomingMessage> listener;

        /// <summary>
        /// Initializes instance of a network peer server.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="ipAddress">Address of the local interface to listen on, or <c>null</c> to listen on all available interfaces.</param>
        /// <param name="internalPort">Port on which the server will listen, or -1 to use the default port for the selected network.</param>
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

            this.AllowLocalPeers = true;
            this.InboundNetworkPeerConnectionParameters = new NetworkPeerConnectionParameters();

            this.LocalEndpoint = Utils.EnsureIPv6(localEndpoint);
            this.ExternalEndpoint = Utils.EnsureIPv6(externalEndpoint);

            this.MaxConnections = 125;
            this.Network = network;
            this.Version = version;

            this.listener = new EventLoopMessageListener<IncomingMessage>(ProcessMessage);
            this.messageProducer = new MessageProducer<IncomingMessage>();
            this.messageProducer.AddMessageListener(this.listener);

            this.ConnectedNetworkPeers = new NetworkPeerCollection();
            this.ConnectedNetworkPeers.MessageProducer.AddMessageListener(this.listener);

            this.serverCancel = new CancellationTokenSource();

            this.tcpListener = new TcpListener(this.LocalEndpoint);
            this.tcpListener.Server.LingerState = new LingerOption(true, 0);
            this.tcpListener.Server.NoDelay = true;
            this.tcpListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

            this.logger.LogTrace("Network peer server ready to listen on '{0}'.", this.LocalEndpoint);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Starts listening on the server's initialialized endpoint.
        /// </summary>
        /// <param name="maxIncoming">Maximal number of newly connected clients waiting to be accepted.</param>
        public void Listen(int maxIncoming = 20)
        {
            this.logger.LogTrace("({0}:{1})", nameof(maxIncoming), maxIncoming);

            try
            {
                this.tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                this.tcpListener.Start();
                this.AcceptClientsAsync();
            }
            catch (Exception e)
            {
                this.logger.LogTrace("Exception occurred: {0}", e.ToString());
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Implements loop accepting new connections.
        /// </summary>
        private async void AcceptClientsAsync()
        {
            this.logger.LogTrace("()");

            this.logger.LogTrace("Accepting incoming connections.");

            while (!this.serverCancel.IsCancellationRequested)
            {
                try
                {
                    int taskCount = Interlocked.Increment(ref this.unfinishedTasks);
                    this.logger.LogTrace("Starting accepting task, there are {0} unfinished tasks now.", taskCount);

                    Socket client = await Task.Run(() => this.tcpListener.AcceptSocketAsync(), this.serverCancel.Token);

                    taskCount = Interlocked.Decrement(ref this.unfinishedTasks);

                    this.logger.LogTrace("Connection accepted from client '{0}', there are {1} unfinished tasks now.", client.RemoteEndPoint, taskCount);
                    Task unused = Task.Run(() => this.ProcessNewClient(client));
                }
                catch (Exception e)
                {
                    if (e is OperationCanceledException) this.logger.LogDebug("Shutdown detected, stop accepting connections.");
                    else this.logger.LogDebug("Exception occurred: {0}", e.ToString());

                    int taskCount = Interlocked.Decrement(ref this.unfinishedTasks);
                    this.logger.LogTrace("Accepting task aborted or failed, there are {0} unfinished tasks now.", taskCount);

                    break;
                }
            }
            
            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Handles a newly accepted client's connection.
        /// </summary>
        /// <param name="client">New client connection's socket.</param>
        private void ProcessNewClient(Socket client)
        {
            this.logger.LogTrace("({0}:{1})", nameof(client), client.RemoteEndPoint);

            EndPoint clientEndPoint = client.RemoteEndPoint;
            int taskCount = Interlocked.Increment(ref this.unfinishedTasks);
            this.logger.LogTrace("Processing new client '{0}', there are {1} unfinished tasks now.", clientEndPoint, taskCount);

            try
            {
                using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(this.serverCancel.Token))
                {
                    cancel.CancelAfter(TimeSpan.FromSeconds(10));

                    var stream = new NetworkStream(client, false);
                    while (true)
                    {
                        if (this.ConnectedNetworkPeers.Count >= this.MaxConnections)
                        {
                            this.logger.LogDebug("Maximum number of connections {0} reached.", this.MaxConnections);
                            Utils.SafeCloseSocket(client);
                            break;
                        }
                        cancel.Token.ThrowIfCancellationRequested();

                        PerformanceCounter counter;
                        Message message = Message.ReadNext(stream, this.Network, this.Version, cancel.Token, out counter);
                        this.messageProducer.PushMessage(new IncomingMessage()
                        {
                            Socket = client,
                            Message = message,
                            Length = counter.ReadBytes,
                            NetworkPeer = null,
                        });

                        if (message.Payload is VersionPayload)
                        {
                            this.logger.LogTrace("Connection with client '{0}' successfully initiated.", client.RemoteEndPoint);
                            break;
                        }

                        this.logger.LogTrace("The first message of the remote peer '{0}' did not contain a version payload.", client.RemoteEndPoint);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (!this.serverCancel.Token.IsCancellationRequested)
                    this.logger.LogTrace("Inbound client '{0}' failed to send a message within 10 seconds, dropping connection.", client.RemoteEndPoint);

                Utils.SafeCloseSocket(client);
            }
            catch (Exception ex)
            {
                if (this.serverCancel.IsCancellationRequested)
                    return;

                if (client == null)
                {
                    this.logger.LogTrace("Exception occurred while accepting connection: {0}", ex.ToString());
                    Thread.Sleep(3000);
                }
                else
                {
                    this.logger.LogTrace("Exception occurred while processing message from client '{0}': {1}", client.RemoteEndPoint, ex.ToString());
                    Utils.SafeCloseSocket(client);
                }
            }

            taskCount = Interlocked.Decrement(ref this.unfinishedTasks);
            this.logger.LogTrace("Processing of new client '{0}' is complete, there are {1} unfinished tasks now.", clientEndPoint, taskCount);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Callback that is called when a new message is received from a connected client peer.
        /// </summary>
        /// <param name="message">Message received from the client.</param>
        private void ProcessMessage(IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(message), message.Message.Command);

            this.ProcessMessageCore(message);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes a new message received from a connected client peer.
        /// </summary>
        /// <param name="message">Message received from the client.</param>
        private void ProcessMessageCore(IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(message), message.Message.Command);

            if (message.Message.Payload is VersionPayload)
            {
                VersionPayload version = message.AssertPayload<VersionPayload>();
                bool connectedToSelf = version.Nonce == this.Nonce;

                if (connectedToSelf) this.logger.LogDebug("Connection to self detected and will be aborted.");

                if ((message.NetworkPeer != null) && connectedToSelf)
                {
                    message.NetworkPeer.DisconnectWithException();

                    this.logger.LogTrace("(-)[CONNECTED_TO_SELF]");
                    return;
                }

                if (message.NetworkPeer == null)
                {
                    this.logger.LogDebug("First message received from peer '{0}'.", version.AddressFrom);

                    IPEndPoint remoteEndpoint = version.AddressFrom;
                    if (!remoteEndpoint.Address.IsRoutable(this.AllowLocalPeers))
                    {
                        // Send his own endpoint.
                        remoteEndpoint = new IPEndPoint(((IPEndPoint)message.Socket.RemoteEndPoint).Address, this.Network.DefaultPort);
                    }

                    var peerAddress = new NetworkAddress()
                    {
                        Endpoint = remoteEndpoint,
                        Time = this.dateTimeProvider.GetUtcNow()
                    };

                    NetworkPeer networkPeer = this.networkPeerFactory.CreateNetworkPeer(peerAddress, this.Network, CreateNetworkPeerConnectionParameters(), message.Socket, version);
                    if (connectedToSelf)
                    {
                        networkPeer.SendMessage(CreateNetworkPeerConnectionParameters().CreateVersion(networkPeer.PeerAddress.Endpoint, this.Network, this.dateTimeProvider.GetTimeOffset()));
                        networkPeer.Disconnect();

                        this.logger.LogTrace("(-)[CONNECTED_TO_SELF_2]");
                        return;
                    }

                    using (CancellationTokenSource cancel = new CancellationTokenSource())
                    {
                        cancel.CancelAfter(TimeSpan.FromSeconds(10.0));
                        try
                        {
                            this.ConnectedNetworkPeers.Add(networkPeer);
                            networkPeer.StateChanged += Peer_StateChanged;
                            networkPeer.RespondToHandShake(cancel.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            this.logger.LogTrace("Remote peer haven't responded within 10 seconds of the handshake completion, dropping connection.");

                            networkPeer.DisconnectWithException();

                            this.logger.LogTrace("(-)[HANDSHAKE_TIMEDOUT]");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogTrace("Exception occurred: {0}", ex.ToString());

                            networkPeer.DisconnectWithException();

                            this.logger.LogTrace("(-)[HANDSHAKE_EXCEPTION]");
                            throw;
                        }
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Callback that is called when a network status of a connected client peer changes.
        /// <para>The peer is removed from the list of connected peers if the connection has been terminated for any reason.</para>
        /// </summary>
        /// <param name="peer">The connected peer.</param>
        /// <param name="oldState">Previous state of the peer. New state of the peer is stored in its <see cref="NetworkPeer.State"/> property.</param>
        private void Peer_StateChanged(NetworkPeer peer, NetworkPeerState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}.{5}:{6})", nameof(peer), peer.PeerAddress, nameof(oldState), oldState, nameof(peer), nameof(peer.State), peer.State);

            if ((peer.State == NetworkPeerState.Disconnecting)
                || (peer.State == NetworkPeerState.Failed)
                || (peer.State == NetworkPeerState.Offline))
                this.ConnectedNetworkPeers.Remove(peer);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.serverCancel.Cancel();

            this.logger.LogTrace("Stopping network peer server.");
            this.listener.Dispose();

            try
            {
                this.ConnectedNetworkPeers.DisconnectAll();
            }
            catch (Exception e)
            {
                this.logger.LogTrace("Exception occurred: {0}", e.ToString());
            }

            this.tcpListener?.Stop();

            this.logger.LogTrace("Waiting for {0} unfinished tasks to complete.", this.unfinishedTasks);
            while (this.unfinishedTasks > 0)
                Thread.Sleep(100);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Initializes connection parameters using the server's initialized values.
        /// </summary>
        /// <returns>Initialized connection parameters.</returns>
        private NetworkPeerConnectionParameters CreateNetworkPeerConnectionParameters()
        {
            IPEndPoint myExternal = Utils.EnsureIPv6(this.ExternalEndpoint);
            NetworkPeerConnectionParameters param2 = this.InboundNetworkPeerConnectionParameters.Clone();
            param2.Nonce = this.Nonce;
            param2.Version = this.Version;
            param2.AddressFrom = myExternal;
            return param2;
        }
    }
}