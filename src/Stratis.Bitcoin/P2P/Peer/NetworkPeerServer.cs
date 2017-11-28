using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    public delegate void NetworkPeerServerNodeEventHandler(NetworkPeerServer sender, NetworkPeer peer);
    public delegate void NetworkPeerServerMessageEventHandler(NetworkPeerServer sender, IncomingMessage message);

    public class NetworkPeerServer : IDisposable
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Factory for creating P2P network peer clients and servers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; private set; }

        public ProtocolVersion Version { get; private set; }

        /// <summary>The parameters that will be cloned and applied for each peer connecting to <see cref="NetworkPeerServer"/>.</summary>
        public NetworkPeerConnectionParameters InboundNetworkPeerConnectionParameters { get; set; }

        public bool AllowLocalPeers { get; set; }

        public int MaxConnections { get; set; }

        private IPEndPoint localEndpoint;
        public IPEndPoint LocalEndpoint
        {
            get
            {
                return this.localEndpoint;
            }
            set
            {
                this.localEndpoint = Utils.EnsureIPv6(value);
            }
        }

        private Socket socket;
        private TraceCorrelation trace;

        public bool IsListening
        {
            get
            {
                return this.socket != null;
            }
        }


        internal readonly MessageProducer<IncomingMessage> messageProducer = new MessageProducer<IncomingMessage>();
        internal readonly MessageProducer<object> internalMessageProducer = new MessageProducer<object>();

        public MessageProducer<IncomingMessage> AllMessages { get; private set; }

        volatile IPEndPoint externalEndpoint;
        public IPEndPoint ExternalEndpoint
        {
            get
            {
                return this.externalEndpoint;
            }
            set
            {
                this.externalEndpoint = Utils.EnsureIPv6(value);
            }
        }

        public NetworkPeerCollection ConnectedNetworkPeers { get; private set; }

        private List<IDisposable> resources = new List<IDisposable>();

        private CancellationTokenSource cancel = new CancellationTokenSource();

        private ulong nonce;
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

        public event NetworkPeerServerNodeEventHandler PeerRemoved;
        public event NetworkPeerServerNodeEventHandler PeerAdded;
        public event NetworkPeerServerMessageEventHandler MessageReceived;

        /// <summary>
        /// Initializes instance of a network peer server.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="version">Version of the network protocol that the server should run.</param>
        /// <param name="internalPort">Port on which the server will listen, or -1 to use the default port for the selected network.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="networkPeerFactory">Factory for creating P2P network peer clients and servers.</param>
        public NetworkPeerServer(Network network, ProtocolVersion version, int internalPort, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INetworkPeerFactory networkPeerFactory)
        {
            internalPort = internalPort == -1 ? network.DefaultPort : internalPort;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{internalPort}] ");
            this.dateTimeProvider = dateTimeProvider;
            this.networkPeerFactory = networkPeerFactory;

            this.AllowLocalPeers = true;
            this.InboundNetworkPeerConnectionParameters = new NetworkPeerConnectionParameters();

            this.localEndpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0").MapToIPv6Ex(), internalPort);

            this.MaxConnections = 125;
            this.Network = network;
            this.externalEndpoint = new IPEndPoint(this.localEndpoint.Address, this.Network.DefaultPort);
            this.Version = version;

            var listener = new EventLoopMessageListener<IncomingMessage>(ProcessMessage);
            this.messageProducer.AddMessageListener(listener);
            this.OwnResource(listener);

            this.ConnectedNetworkPeers = new NetworkPeerCollection();
            this.ConnectedNetworkPeers.Added += Peers_PeerAdded;
            this.ConnectedNetworkPeers.Removed += Peers_PeerRemoved;
            this.ConnectedNetworkPeers.MessageProducer.AddMessageListener(listener);

            this.AllMessages = new MessageProducer<IncomingMessage>();
            this.trace = new TraceCorrelation(NodeServerTrace.Trace, "Network peer server listening on " + this.LocalEndpoint);
        }

        private void Peers_PeerRemoved(object sender, NetworkPeerEventArgs eventArgs)
        {
            this.PeerRemoved?.Invoke(this, eventArgs.peer);
        }

        private void Peers_PeerAdded(object sender, NetworkPeerEventArgs eventArgs)
        {
            this.PeerAdded?.Invoke(this, eventArgs.peer);
        }

        public void Listen(int maxIncoming = 8)
        {
            if (this.socket != null)
                throw new InvalidOperationException("Already listening");

            using (this.trace.Open())
            {
                try
                {
                    this.socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                    this.socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

                    this.socket.Bind(this.LocalEndpoint);
                    this.socket.Listen(maxIncoming);
                    NodeServerTrace.Information("Listening...");
                    this.BeginAccept();
                }
                catch (Exception ex)
                {
                    NodeServerTrace.Error("Error while opening the Protocol server", ex);
                    throw;
                }
            }
        }

        private void BeginAccept()
        {
            if (this.cancel.IsCancellationRequested)
            {
                NodeServerTrace.Information("Stop accepting connection...");
                return;
            }

            NodeServerTrace.Information("Accepting connection...");
            var args = new SocketAsyncEventArgs();
            args.Completed += Accept_Completed;
            if (!this.socket.AcceptAsync(args))
                this.EndAccept(args);
        }

        private void Accept_Completed(object sender, SocketAsyncEventArgs e)
        {
            this.EndAccept(e);
        }

        private void EndAccept(SocketAsyncEventArgs args)
        {
            using (this.trace.Open())
            {
                Socket client = null;
                try
                {
                    if (args.SocketError != SocketError.Success)
                        throw new SocketException((int)args.SocketError);

                    client = args.AcceptSocket;
                    if (this.cancel.IsCancellationRequested)
                        return;

                    NodeServerTrace.Information("Client connection accepted : " + client.RemoteEndPoint);
                    var cancel = CancellationTokenSource.CreateLinkedTokenSource(this.cancel.Token);
                    cancel.CancelAfter(TimeSpan.FromSeconds(10));

                    var stream = new NetworkStream(client, false);
                    while (true)
                    {
                        if (this.ConnectedNetworkPeers.Count >= this.MaxConnections)
                        {
                            NodeServerTrace.Information("MaxConnections limit reached");
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
                            break;

                        NodeServerTrace.Error("The first message of the remote peer did not contained a Version payload", null);
                    }
                }
                catch (OperationCanceledException)
                {
                    Utils.SafeCloseSocket(client);
                    if (!this.cancel.Token.IsCancellationRequested)
                    {
                        NodeServerTrace.Error("The remote connecting failed to send a message within 10 seconds, dropping connection", null);
                    }
                }
                catch (Exception ex)
                {
                    if (this.cancel.IsCancellationRequested)
                        return;

                    if (client == null)
                    {
                        NodeServerTrace.Error("Error while accepting connection ", ex);
                        Thread.Sleep(3000);
                    }
                    else
                    {
                        Utils.SafeCloseSocket(client);
                        NodeServerTrace.Error("Invalid message received from the remote connecting peer", ex);
                    }
                }

                this.BeginAccept();
            }
        }

        internal void ExternalAddressDetected(IPAddress ipAddress)
        {
            if (!this.ExternalEndpoint.Address.IsRoutable(this.AllowLocalPeers) && ipAddress.IsRoutable(this.AllowLocalPeers))
            {
                NodeServerTrace.Information("New externalAddress detected " + ipAddress);
                this.ExternalEndpoint = new IPEndPoint(ipAddress, this.ExternalEndpoint.Port);
            }
        }

        private void ProcessMessage(IncomingMessage message)
        {
            this.AllMessages.PushMessage(message);
            TraceCorrelation trace = null;
            if (message.NetworkPeer != null)
            {
                trace = message.NetworkPeer.TraceCorrelation;
            }
            else
            {
                trace = new TraceCorrelation(NodeServerTrace.Trace, "Processing inbound message " + message.Message);
            }

            using (trace.Open(false))
            {
                this.ProcessMessageCore(message);
            }
        }

        private void ProcessMessageCore(IncomingMessage message)
        {
            if (message.Message.Payload is VersionPayload)
            {
                VersionPayload version = message.AssertPayload<VersionPayload>();
                bool connectedToSelf = version.Nonce == this.Nonce;
                if ((message.NetworkPeer != null) && connectedToSelf)
                {
                    NodeServerTrace.ConnectionToSelfDetected();
                    message.NetworkPeer.DisconnectAsync();
                    return;
                }

                if (message.NetworkPeer == null)
                {
                    IPEndPoint remoteEndpoint = version.AddressFrom;
                    if (!remoteEndpoint.Address.IsRoutable(this.AllowLocalPeers))
                    {
                        // Send his own endpoint.
                        remoteEndpoint = new IPEndPoint(((IPEndPoint)message.Socket.RemoteEndPoint).Address, this.Network.DefaultPort);
                    }

                    var peerAddress = new NetworkAddress()
                    {
                        Endpoint = remoteEndpoint,
                        Time = DateTimeOffset.UtcNow
                    };

                    NetworkPeer networkPeer = this.networkPeerFactory.CreateNetworkPeer(peerAddress, this.Network, CreateNetworkPeerConnectionParameters(), message.Socket, version);
                    if (connectedToSelf)
                    {
                        networkPeer.SendMessage(CreateNetworkPeerConnectionParameters().CreateVersion(networkPeer.PeerAddress.Endpoint, this.Network));
                        NodeServerTrace.ConnectionToSelfDetected();
                        networkPeer.Disconnect();
                        return;
                    }

                    CancellationTokenSource cancel = new CancellationTokenSource();
                    cancel.CancelAfter(TimeSpan.FromSeconds(10.0));
                    try
                    {
                        this.ConnectedNetworkPeers.Add(networkPeer);
                        networkPeer.StateChanged += Peer_StateChanged;
                        networkPeer.RespondToHandShake(cancel.Token);
                    }
                    catch (OperationCanceledException ex)
                    {
                        NodeServerTrace.Error("The remote peer did not respond fast enough (10 seconds) to the handshake completion, dropping connection", ex);
                        networkPeer.DisconnectAsync();
                        throw;
                    }
                    catch (Exception)
                    {
                        networkPeer.DisconnectAsync();
                        throw;
                    }
                }
            }

            this.MessageReceived?.Invoke(this, message);
        }

        private void Peer_StateChanged(NetworkPeer peer, NetworkPeerState oldState)
        {
            if ((peer.State == NetworkPeerState.Disconnecting)
                || (peer.State == NetworkPeerState.Failed)
                || (peer.State == NetworkPeerState.Offline))
                this.ConnectedNetworkPeers.Remove(peer);
        }

        IDisposable OwnResource(IDisposable resource)
        {
            if (this.cancel.IsCancellationRequested)
            {
                resource.Dispose();
                return Scope.Nothing;
            }

            return new Scope(() =>
            {
                lock (this.resources)
                {
                    this.resources.Add(resource);
                }
            }, () =>
            {
                lock (this.resources)
                {
                    this.resources.Remove(resource);
                }
            });
        }

        public void Dispose()
        {
            if (!this.cancel.IsCancellationRequested)
            {
                this.cancel.Cancel();
                this.trace.LogInside(() => NodeServerTrace.Information("Stopping network peer server..."));
                lock (this.resources)
                {
                    foreach (IDisposable resource in this.resources)
                        resource.Dispose();
                }

                try
                {
                    this.ConnectedNetworkPeers.DisconnectAll();
                }
                finally
                {
                    if (this.socket != null)
                    {
                        Utils.SafeCloseSocket(this.socket);
                        this.socket = null;
                    }
                }
            }
        }

        internal NetworkPeerConnectionParameters CreateNetworkPeerConnectionParameters()
        {
            IPEndPoint myExternal = Utils.EnsureIPv6(this.ExternalEndpoint);
            NetworkPeerConnectionParameters param2 = this.InboundNetworkPeerConnectionParameters.Clone();
            param2.Nonce = this.Nonce;
            param2.Version = this.Version;
            param2.AddressFrom = myExternal;
            return param2;
        }

        public bool IsConnectedTo(IPEndPoint endpoint)
        {
            return this.ConnectedNetworkPeers.FindByEndpoint(endpoint) != null;
        }

        public NetworkPeer FindOrConnect(IPEndPoint endpoint)
        {
            while (true)
            {
                NetworkPeer peer = this.ConnectedNetworkPeers.FindByEndpoint(endpoint);
                if (peer != null)
                    return peer;

                peer = this.networkPeerFactory.CreateConnectedNetworkPeer(this.Network, endpoint, CreateNetworkPeerConnectionParameters());
                peer.StateChanged += Peer_StateChanged;
                if (!this.ConnectedNetworkPeers.Add(peer))
                {
                    peer.DisconnectAsync();
                }
                else return peer;
            }
        }
    }
}