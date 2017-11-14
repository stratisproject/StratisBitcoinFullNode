using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NBitcoin.Protocol
{
    public delegate void NodeServerNodeEventHandler(NodeServer sender, Node node);
    public delegate void NodeServerMessageEventHandler(NodeServer sender, IncomingMessage message);

    public class NodeServer : IDisposable
    {
        public Network Network { get; private set; }

        public ProtocolVersion Version { get; private set; }

        /// <summary>The parameters that will be cloned and applied for each node connecting to <see cref="NodeServer"/>.</summary>
        public NodeConnectionParameters InboundNodeConnectionParameters { get; set; }

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

        public NodesCollection ConnectedNodes { get; private set; }

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

        public event NodeServerNodeEventHandler NodeRemoved;
        public event NodeServerNodeEventHandler NodeAdded;
        public event NodeServerMessageEventHandler MessageReceived;

        public NodeServer(Network network, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, int internalPort = -1)
        {
            this.AllowLocalPeers = true;
            this.InboundNodeConnectionParameters = new NodeConnectionParameters();

            internalPort = internalPort == -1 ? network.DefaultPort : internalPort;
            this.localEndpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0").MapToIPv6Ex(), internalPort);

            this.MaxConnections = 125;
            this.Network = network;
            this.externalEndpoint = new IPEndPoint(this.localEndpoint.Address, this.Network.DefaultPort);
            this.Version = version;

            var listener = new EventLoopMessageListener<IncomingMessage>(ProcessMessage);
            this.messageProducer.AddMessageListener(listener);
            this.OwnResource(listener);

            this.ConnectedNodes = new NodesCollection();
            this.ConnectedNodes.Added += Nodes_NodeAdded;
            this.ConnectedNodes.Removed += Nodes_NodeRemoved;
            this.ConnectedNodes.MessageProducer.AddMessageListener(listener);

            this.AllMessages = new MessageProducer<IncomingMessage>();
            this.trace = new TraceCorrelation(NodeServerTrace.Trace, "Node server listening on " + this.LocalEndpoint);
        }

        private void Nodes_NodeRemoved(object sender, NodeEventArgs node)
        {
            this.NodeRemoved?.Invoke(this, node.Node);
        }

        private void Nodes_NodeAdded(object sender, NodeEventArgs node)
        {
            this.NodeAdded?.Invoke(this, node.Node);
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
                        if (this.ConnectedNodes.Count >= this.MaxConnections)
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
                            Node = null,
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
                        NodeServerTrace.Error("Invalid message received from the remote connecting node", ex);
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
            if (message.Node != null)
            {
                trace = message.Node.TraceCorrelation;
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
                if ((message.Node != null) && connectedToSelf)
                {
                    NodeServerTrace.ConnectionToSelfDetected();
                    message.Node.DisconnectAsync();
                    return;
                }

                if (message.Node == null)
                {
                    IPEndPoint remoteEndpoint = version.AddressFrom;
                    if (!remoteEndpoint.Address.IsRoutable(this.AllowLocalPeers))
                    {
                        // Send his own endpoint.
                        remoteEndpoint = new IPEndPoint(((IPEndPoint)message.Socket.RemoteEndPoint).Address, this.Network.DefaultPort);
                    }

                    var peer = new NetworkAddress()
                    {
                        Endpoint = remoteEndpoint,
                        Time = DateTimeOffset.UtcNow
                    };

                    var node = new Node(peer, this.Network, CreateNodeConnectionParameters(), message.Socket, version);
                    if (connectedToSelf)
                    {
                        node.SendMessage(CreateNodeConnectionParameters().CreateVersion(node.Peer.Endpoint, this.Network));
                        NodeServerTrace.ConnectionToSelfDetected();
                        node.Disconnect();
                        return;
                    }

                    CancellationTokenSource cancel = new CancellationTokenSource();
                    cancel.CancelAfter(TimeSpan.FromSeconds(10.0));
                    try
                    {
                        this.ConnectedNodes.Add(node);
                        node.StateChanged += Node_StateChanged;
                        node.RespondToHandShake(cancel.Token);
                    }
                    catch (OperationCanceledException ex)
                    {
                        NodeServerTrace.Error("The remote node did not respond fast enough (10 seconds) to the handshake completion, dropping connection", ex);
                        node.DisconnectAsync();
                        throw;
                    }
                    catch (Exception)
                    {
                        node.DisconnectAsync();
                        throw;
                    }
                }
            }

            this.MessageReceived?.Invoke(this, message);
        }

        private void Node_StateChanged(Node node, NodeState oldState)
        {
            if ((node.State == NodeState.Disconnecting)
                || (node.State == NodeState.Failed)
                || (node.State == NodeState.Offline))
                this.ConnectedNodes.Remove(node);
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
                this.trace.LogInside(() => NodeServerTrace.Information("Stopping node server..."));
                lock (this.resources)
                {
                    foreach (IDisposable resource in this.resources)
                        resource.Dispose();
                }

                try
                {
                    this.ConnectedNodes.DisconnectAll();
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

        internal NodeConnectionParameters CreateNodeConnectionParameters()
        {
            IPEndPoint myExternal = Utils.EnsureIPv6(this.ExternalEndpoint);
            NodeConnectionParameters param2 = this.InboundNodeConnectionParameters.Clone();
            param2.Nonce = this.Nonce;
            param2.Version = this.Version;
            param2.AddressFrom = myExternal;
            return param2;
        }

        public bool IsConnectedTo(IPEndPoint endpoint)
        {
            return this.ConnectedNodes.FindByEndpoint(endpoint) != null;
        }

        public Node FindOrConnect(IPEndPoint endpoint)
        {
            while (true)
            {
                Node node = this.ConnectedNodes.FindByEndpoint(endpoint);
                if (node != null)
                    return node;

                node = Node.Connect(this.Network, endpoint, CreateNodeConnectionParameters());
                node.StateChanged += Node_StateChanged;
                if (!this.ConnectedNodes.Add(node))
                {
                    node.DisconnectAsync();
                }
                else return node;
            }
        }
    }
}