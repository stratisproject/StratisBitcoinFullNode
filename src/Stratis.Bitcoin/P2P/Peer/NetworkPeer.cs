using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Filters;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    public enum NetworkPeerState : int
    {
        Failed,
        Offline,
        Disconnecting,
        Connected,
        HandShaked
    }

    public class NodeDisconnectReason
    {
        public string Reason { get; set; }
        public Exception Exception { get; set; }
    }

    public class NodeRequirement
    {
        public ProtocolVersion? MinVersion { get; set; }
        public NodeServices RequiredServices { get; set; }

        public bool SupportSPV { get; set; }

        public virtual bool Check(VersionPayload version)
        {
            if (this.MinVersion != null)
            {
                if (version.Version < this.MinVersion.Value)
                    return false;
            }

            if ((this.RequiredServices & version.Services) != this.RequiredServices)
            {
                return false;
            }

            if (this.SupportSPV)
            {
                if (version.Version < ProtocolVersion.MEMPOOL_GD_VERSION)
                    return false;

                if ((ProtocolVersion.NO_BLOOM_VERSION <= version.Version) && ((version.Services & NodeServices.NODE_BLOOM) == 0))
                    return false;
            }

            return true;
        }
    }

    public delegate void NodeEventHandler(NetworkPeer node);
    public delegate void NodeEventMessageIncoming(NetworkPeer node, IncomingMessage message);
    public delegate void NodeStateEventHandler(NetworkPeer node, NetworkPeerState oldState);

    public class SentMessage
    {
        public Payload Payload;
        public TaskCompletionSource<bool> Completion;
        public Guid ActivityId;
    }

    public class NetworkPeerConnection
    {
        public NetworkPeer Node { get; private set; }

        public Socket Socket { get; private set; }

        public ManualResetEvent Disconnected { get; private set; }

        public CancellationTokenSource Cancel { get; private set; }

        public TraceCorrelation TraceCorrelation
        {
            get
            {
                return this.Node.TraceCorrelation;
            }
        }

        internal BlockingCollection<SentMessage> Messages;

        private int cleaningUp;
        public int ListenerThreadId;

        public NetworkPeerConnection(NetworkPeer node, Socket socket)
        {
            this.Node = node;
            this.Socket = socket;
            this.Messages = new BlockingCollection<SentMessage>(new ConcurrentQueue<SentMessage>());
        }

        public void BeginListen()
        {
            this.Disconnected = new ManualResetEvent(false);
            this.Cancel = new CancellationTokenSource();

            new Thread(() =>
            {
                SentMessage processing = null;
                Exception unhandledException = null;
                bool isVerbose = NodeServerTrace.Trace.Switch.ShouldTrace(TraceEventType.Verbose);

                try
                {
                    using (var completedEvent = new ManualResetEvent(false))
                    {
                        using (var socketEventManager = NodeSocketEventManager.Create(completedEvent))
                        {
                            socketEventManager.SocketEvent.SocketFlags = SocketFlags.None;

                            foreach (SentMessage kv in this.Messages.GetConsumingEnumerable(this.Cancel.Token))
                            {
                                processing = kv;
                                Payload payload = kv.Payload;
                                var message = new Message
                                {
                                    Magic = this.Node.Network.Magic,
                                    Payload = payload
                                };

                                if (isVerbose)
                                {
                                    // NETSTDCONV Trace.CorrelationManager.ActivityId = kv.ActivityId;
                                    if (kv.ActivityId != this.TraceCorrelation.Activity)
                                    {
                                        NodeServerTrace.Transfer(this.TraceCorrelation.Activity);
                                        // NETSTDCONV Trace.CorrelationManager.ActivityId = TraceCorrelation.Activity;
                                    }
                                    NodeServerTrace.Verbose("Sending message " + message);
                                }

                                MemoryStream ms = new MemoryStream();
                                message.ReadWrite(new BitcoinStream(ms, true)
                                {
                                    ProtocolVersion = this.Node.Version,
                                    TransactionOptions = this.Node.SupportedTransactionOptions
                                });

                                byte[] bytes = ms.ToArrayEfficient();
                                socketEventManager.SocketEvent.SetBuffer(bytes, 0, bytes.Length);
                                this.Node.Counter.AddWritten(bytes.Length);
                                completedEvent.Reset();
                                if (!this.Socket.SendAsync(socketEventManager.SocketEvent))
                                    Utils.SafeSet(completedEvent);

                                WaitHandle.WaitAny(new WaitHandle[] { completedEvent, this.Cancel.Token.WaitHandle }, -1);
                                if (!this.Cancel.Token.IsCancellationRequested)
                                {
                                    if (socketEventManager.SocketEvent.SocketError != SocketError.Success)
                                        throw new SocketException((int)socketEventManager.SocketEvent.SocketError);

                                    processing.Completion.SetResult(true);
                                    processing = null;
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    unhandledException = ex;
                }

                if (processing != null)
                    this.Messages.Add(processing);

                foreach (SentMessage pending in this.Messages)
                {
                    if (isVerbose)
                    {
                        // NETSTDCONV Trace.CorrelationManager.ActivityId = pending.ActivityId;
                        if ((pending != processing) && (pending.ActivityId != this.TraceCorrelation.Activity))
                            NodeServerTrace.Transfer(this.TraceCorrelation.Activity);
                        // NETSTDCONV Trace.CorrelationManager.ActivityId = TraceCorrelation.Activity;
                        NodeServerTrace.Verbose("The connection cancelled before the message was sent");
                    }
                    pending.Completion.SetException(new OperationCanceledException("The peer has been disconnected"));
                }

                this.Messages = new BlockingCollection<SentMessage>(new ConcurrentQueue<SentMessage>());
                NodeServerTrace.Information("Stop sending");
                this.EndListen(unhandledException);
            }).Start();

            new Thread(() =>
            {
                this.ListenerThreadId = Thread.CurrentThread.ManagedThreadId;
                using (this.TraceCorrelation.Open(false))
                {
                    NodeServerTrace.Information("Listening");
                    Exception unhandledException = null;
                    byte[] buffer = this.Node.ReuseBuffer ? new byte[1024 * 1024] : null;
                    try
                    {
                        var stream = new NetworkStream(this.Socket, false);
                        while (!this.Cancel.Token.IsCancellationRequested)
                        {
                            PerformanceCounter counter;

                            Message message = Message.ReadNext(stream, this.Node.Network, this.Node.Version, this.Cancel.Token, buffer, out counter);
                            if (NodeServerTrace.Trace.Switch.ShouldTrace(TraceEventType.Verbose))
                                NodeServerTrace.Verbose("Receiving message : " + message.Command + " (" + message.Payload + ")");

                            this.Node.LastSeen = DateTimeOffset.UtcNow;
                            this.Node.Counter.Add(counter);
                            this.Node.OnMessageReceived(new IncomingMessage()
                            {
                                Message = message,
                                Socket = this.Socket,
                                Length = counter.ReadBytes,
                                Node = this.Node
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        unhandledException = ex;
                    }
                    NodeServerTrace.Information("Stop listening");
                    this.EndListen(unhandledException);
                }
            }).Start();
        }

        private void EndListen(Exception unhandledException)
        {
            if (Interlocked.CompareExchange(ref this.cleaningUp, 1, 0) == 1)
                return;

            if (!this.Cancel.IsCancellationRequested)
            {
                NodeServerTrace.Error("Connection to server stopped unexpectedly", unhandledException);
                this.Node.DisconnectReason = new NodeDisconnectReason()
                {
                    Reason = "Unexpected exception while connecting to socket",
                    Exception = unhandledException
                };
                this.Node.State = NetworkPeerState.Failed;
            }

            if (this.Node.State != NetworkPeerState.Failed)
                this.Node.State = NetworkPeerState.Offline;

            if (this.Cancel.IsCancellationRequested == false)
                this.Cancel.Cancel();

            if (this.Disconnected.GetSafeWaitHandle().IsClosed == false)
                this.Disconnected.Set();

            Utils.SafeCloseSocket(this.Socket);

            foreach (INodeBehavior behavior in this.Node.Behaviors)
            {
                try
                {
                    behavior.Detach();
                }
                catch (Exception ex)
                {
                    NodeServerTrace.Error("Error while detaching behavior " + behavior.GetType().FullName, ex);
                }
            }
        }

        internal void CleanUp()
        {
            this.Disconnected.Dispose();
            this.Cancel.Dispose();
        }
    }

    public class NetworkPeer : IDisposable
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public DateTimeOffset ConnectedAt { get; private set; }

        private volatile NetworkPeerState state = NetworkPeerState.Offline;
        public NetworkPeerState State
        {
            get
            {
                return this.state;
            }
            set
            {
                this.TraceCorrelation.LogInside(() => NodeServerTrace.Information("State changed from " + this.state + " to " + value));
                NetworkPeerState previous = this.state;
                this.state = value;
                if (previous != this.state)
                {
                    this.OnStateChanged(previous);
                    if (value == NetworkPeerState.Failed || value == NetworkPeerState.Offline)
                    {
                        // NETSTDCONV
                        // TraceCorrelation.LogInside(() => NodeServerTrace.Trace.TraceEvent(TraceEventType.Stop, 0, "Communication closed"));
                        this.TraceCorrelation.LogInside(() => NodeServerTrace.Trace.TraceEvent(TraceEventType.Critical, 0, "Communication closed"));
                        this.OnDisconnected();
                    }
                }
            }
        }

        public IPAddress RemoteSocketAddress { get; private set; }
        public IPEndPoint RemoteSocketEndpoint { get; private set; }
        public int RemoteSocketPort { get; private set; }
        public bool Inbound { get; private set; }

        public bool ReuseBuffer;
        public NodeBehaviorsCollection Behaviors { get; private set; }
        public NetworkAddress PeerAddress { get; private set; }

        public DateTimeOffset LastSeen { get; set; }

        public TimeSpan? TimeOffset { get; private set; }

        private TraceCorrelation traceCorrelation = null;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public TraceCorrelation TraceCorrelation
        {
            get
            {
                if (this.traceCorrelation == null)
                {
                    this.traceCorrelation = new TraceCorrelation(NodeServerTrace.Trace, "Communication with " + this.PeerAddress.Endpoint.ToString());
                }

                return this.traceCorrelation;
            }
        }

        internal readonly NetworkPeerConnection connection;

        private PerformanceCounter counter;
        public PerformanceCounter Counter
        {
            get
            {
                if (this.counter == null)
                    this.counter = new PerformanceCounter();

                return this.counter;
            }
        }

        /// <summary>
        /// The negociated protocol version (minimum of supported version between MyVersion and the PeerVersion).
        /// </summary>
        public ProtocolVersion Version
        {
            get
            {
                ProtocolVersion peerVersion = this.PeerVersion == null ? this.MyVersion.Version : this.PeerVersion.Version;
                ProtocolVersion myVersion = this.MyVersion.Version;
                uint min = Math.Min((uint)peerVersion, (uint)myVersion);
                return (ProtocolVersion)min;
            }
        }

        public bool IsConnected
        {
            get
            {
                return (this.State == NetworkPeerState.Connected) || (this.State == NetworkPeerState.HandShaked);
            }
        }

        public MessageProducer<IncomingMessage> MessageProducer { get; private set; } = new MessageProducer<IncomingMessage>();
        public NodeFiltersCollection Filters { get; private set; } = new NodeFiltersCollection();

        /// <summary>Send addr unsollicited message of the AddressFrom peer when passing to Handshaked state.</summary>
        public bool Advertize { get; set; }

        public VersionPayload MyVersion { get; private set; }

        public VersionPayload PeerVersion { get; private set; }

        private int disconnecting;

        /// <summary>Transaction options we would like.</summary>
        public TransactionOptions PreferredTransactionOptions { get; private set; } = TransactionOptions.All;

        /// <summary>Transaction options supported by the peer.</summary>
        public TransactionOptions SupportedTransactionOptions { get; private set; } = TransactionOptions.None;

        /// <summary>Transaction options we prefer and which is also supported by peer.</summary>
        public TransactionOptions ActualTransactionOptions
        {
            get
            {
                return this.PreferredTransactionOptions & this.SupportedTransactionOptions;
            }
        }

        public NodeDisconnectReason DisconnectReason { get; set; }

        private Socket Socket
        {
            get
            {
                return this.connection.Socket;
            }
        }

        private TimeSpan pollHeaderDelay = TimeSpan.FromMinutes(1.0);

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; set; }

        public event NodeStateEventHandler StateChanged;
        public event NodeEventMessageIncoming MessageReceived;
        public event NodeEventHandler Disconnected;

        public NetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.RemoteSocketEndpoint}] ");
            this.dateTimeProvider = dateTimeProvider;

            parameters = parameters ?? new NetworkPeerConnectionParameters();
            this.Inbound = false;
            this.Behaviors = new NodeBehaviorsCollection(this);
            this.MyVersion = parameters.CreateVersion(peerAddress.Endpoint, network);
            this.Network = network;
            this.PeerAddress = peerAddress;
            this.LastSeen = peerAddress.Time;

            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

            this.connection = new NetworkPeerConnection(this, socket);

            socket.ReceiveBufferSize = parameters.ReceiveBufferSize;
            socket.SendBufferSize = parameters.SendBufferSize;
            using (this.TraceCorrelation.Open())
            {
                try
                {
                    using (var completedEvent = new ManualResetEvent(false))
                    {
                        using (var nodeSocketEventManager = NodeSocketEventManager.Create(completedEvent, peerAddress.Endpoint))
                        {
                            // If the socket connected straight away (synchronously) unblock all threads.
                            if (!socket.ConnectAsync(nodeSocketEventManager.SocketEvent))
                                completedEvent.Set();

                            // Otherwise wait for the socket connection to complete OR if the operation got cancelled.
                            WaitHandle.WaitAny(new WaitHandle[] { completedEvent, parameters.ConnectCancellation.WaitHandle });

                            parameters.ConnectCancellation.ThrowIfCancellationRequested();

                            if (nodeSocketEventManager.SocketEvent.SocketError != SocketError.Success)
                                throw new SocketException((int)nodeSocketEventManager.SocketEvent.SocketError);

                            var remoteEndpoint = (IPEndPoint)(socket.RemoteEndPoint ?? nodeSocketEventManager.SocketEvent.RemoteEndPoint);
                            this.RemoteSocketAddress = remoteEndpoint.Address;
                            this.RemoteSocketEndpoint = remoteEndpoint;
                            this.RemoteSocketPort = remoteEndpoint.Port;

                            this.State = NetworkPeerState.Connected;
                            this.ConnectedAt = DateTimeOffset.UtcNow;

                            NodeServerTrace.Information("Outbound connection successful.");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Utils.SafeCloseSocket(socket);
                    NodeServerTrace.Information("Connection to node cancelled");
                    this.State = NetworkPeerState.Offline;

                    throw;
                }
                catch (Exception ex)
                {
                    Utils.SafeCloseSocket(socket);
                    NodeServerTrace.Error("Error connecting to the remote endpoint ", ex);
                    this.DisconnectReason = new NodeDisconnectReason()
                    {
                        Reason = "Unexpected exception while connecting to socket",
                        Exception = ex
                    };

                    this.State = NetworkPeerState.Failed;

                    throw;
                }

                this.InitDefaultBehaviors(parameters);
                this.connection.BeginListen();
            }
        }

        public NetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, Socket socket, VersionPayload peerVersion, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.RemoteSocketEndpoint = ((IPEndPoint)socket.RemoteEndPoint);
            this.RemoteSocketAddress = ((IPEndPoint)socket.RemoteEndPoint).Address;
            this.RemoteSocketPort = ((IPEndPoint)socket.RemoteEndPoint).Port;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.RemoteSocketEndpoint}] ");
            this.dateTimeProvider = dateTimeProvider;

            this.Inbound = true;
            this.Behaviors = new NodeBehaviorsCollection(this);
            this.MyVersion = parameters.CreateVersion(peerAddress.Endpoint, network);
            this.Network = network;
            this.PeerAddress = peerAddress;
            this.connection = new NetworkPeerConnection(this, socket);
            this.PeerVersion = peerVersion;
            this.LastSeen = peerAddress.Time;
            this.ConnectedAt = DateTimeOffset.UtcNow;
            this.TraceCorrelation.LogInside((Action)(() =>
            {
                NodeServerTrace.Information((string)("Connected to advertised node " + this.PeerAddress.Endpoint));
                this.State = NetworkPeerState.Connected;
            }));
            this.InitDefaultBehaviors(parameters);
            this.connection.BeginListen();
        }

        private void OnStateChanged(NetworkPeerState previous)
        {
            NodeStateEventHandler stateChanged = StateChanged;
            if (stateChanged != null)
            {
                foreach (NodeStateEventHandler handler in stateChanged.GetInvocationList().Cast<NodeStateEventHandler>())
                {
                    try
                    {
                        handler.DynamicInvoke(this, previous);
                    }
                    catch (TargetInvocationException ex)
                    {
                        this.TraceCorrelation.LogInside(() => NodeServerTrace.Error("Error while StateChanged event raised", ex.InnerException));
                    }
                }
            }
        }

        public void OnMessageReceived(IncomingMessage message)
        {
            var version = message.Message.Payload as VersionPayload;
            if ((version != null) && (this.State == NetworkPeerState.HandShaked))
            {
                if (message.Node.Version >= ProtocolVersion.REJECT_VERSION)
                    message.Node.SendMessageAsync(new RejectPayload()
                    {
                        Code = RejectCode.DUPLICATE
                    });
            }

            if (version != null)
            {
                this.TimeOffset = DateTimeOffset.Now - version.Timestamp;
                if ((version.Services & NodeServices.NODE_WITNESS) != 0)
                    this.SupportedTransactionOptions |= TransactionOptions.Witness;
            }

            if (message.Message.Payload is HaveWitnessPayload)
                this.SupportedTransactionOptions |= TransactionOptions.Witness;

            var last = new ActionFilter((m, n) =>
            {
                this.MessageProducer.PushMessage(m);
                NodeEventMessageIncoming messageReceived = MessageReceived;
                if (messageReceived != null)
                {
                    foreach (NodeEventMessageIncoming handler in messageReceived.GetInvocationList().Cast<NodeEventMessageIncoming>())
                    {
                        try
                        {
                            handler.DynamicInvoke(this, m);
                        }
                        catch (TargetInvocationException ex)
                        {
                            this.TraceCorrelation.LogInside(() => NodeServerTrace.Error("Error while OnMessageReceived event raised", ex.InnerException), false);
                        }
                    }
                }
            });

            IEnumerator<INodeFilter> enumerator = this.Filters.Concat(new[] { last }).GetEnumerator();
            this.FireFilters(enumerator, message);
        }

        private void OnSendingMessage(Payload payload, Action final)
        {
            IEnumerator<INodeFilter> enumerator = this.Filters.Concat(new[] { new ActionFilter(null, (n, p, a) => final()) }).GetEnumerator();
            this.FireFilters(enumerator, payload);
        }

        private void FireFilters(IEnumerator<INodeFilter> enumerator, Payload payload)
        {
            if (enumerator.MoveNext())
            {
                INodeFilter filter = enumerator.Current;
                try
                {
                    filter.OnSendingMessage(this, payload, () => FireFilters(enumerator, payload));
                }
                catch (Exception ex)
                {
                    this.TraceCorrelation.LogInside(() => NodeServerTrace.Error("Unhandled exception raised by a node filter (OnSendingMessage)", ex.InnerException), false);
                }
            }
        }

        private void FireFilters(IEnumerator<INodeFilter> enumerator, IncomingMessage message)
        {
            if (enumerator.MoveNext())
            {
                INodeFilter filter = enumerator.Current;
                try
                {
                    filter.OnReceivingMessage(message, () => FireFilters(enumerator, message));
                }
                catch (Exception ex)
                {
                    this.TraceCorrelation.LogInside(() => NodeServerTrace.Error("Unhandled exception raised by a node filter (OnReceivingMessage)", ex.InnerException), false);
                }
            }
        }

        private void OnDisconnected()
        {
            NodeEventHandler disconnected = Disconnected;
            if (disconnected != null)
            {
                foreach (NodeEventHandler handler in disconnected.GetInvocationList().Cast<NodeEventHandler>())
                {
                    try
                    {
                        handler.DynamicInvoke(this);
                    }
                    catch (TargetInvocationException ex)
                    {
                        this.TraceCorrelation.LogInside(() => NodeServerTrace.Error("Error while Disconnected event raised", ex.InnerException));
                    }
                }
            }
        }

        private void InitDefaultBehaviors(NetworkPeerConnectionParameters parameters)
        {
            this.Advertize = parameters.Advertize;
            this.PreferredTransactionOptions = parameters.PreferredTransactionOptions;
            this.ReuseBuffer = parameters.ReuseBuffer;

            this.Behaviors.DelayAttach = true;
            foreach (INodeBehavior behavior in parameters.TemplateBehaviors)
            {
                this.Behaviors.Add(behavior.Clone());
            }

            this.Behaviors.DelayAttach = false;
        }

        /// <summary>
        /// Send a message to the peer asynchronously.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <param name="System.OperationCanceledException">The node has been disconnected.</param>
        public Task SendMessageAsync(Payload payload)
        {
            if (payload == null)
                throw new ArgumentNullException("payload");

            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            if (!this.IsConnected)
            {
                completion.SetException(new OperationCanceledException("The peer has been disconnected"));
                return completion.Task;
            }
            // NETSTDCONV
            // var activity = Trace.CorrelationManager.ActivityId;

            var activity = Guid.NewGuid();
            Action final = () =>
            {
                this.connection.Messages.Add(new SentMessage()
                {
                    Payload = payload,
                    ActivityId = activity,
                    Completion = completion
                });
            };

            this.OnSendingMessage(payload, final);
            return completion.Task;
        }

        /// <summary>
        /// Send a message to the peer synchronously.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <exception cref="System.ArgumentNullException">Payload is null.</exception>
        /// <param name="System.OperationCanceledException">The node has been disconnected, or the cancellation token has been set to canceled.</param>
        public void SendMessage(Payload payload, CancellationToken cancellation = default(CancellationToken))
        {
            try
            {
                SendMessageAsync(payload).Wait(cancellation);
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                throw;
            }
        }

        public TPayload ReceiveMessage<TPayload>(TimeSpan timeout) where TPayload : Payload
        {
            var source = new CancellationTokenSource();
            source.CancelAfter(timeout);
            return ReceiveMessage<TPayload>(source.Token);
        }

        public TPayload ReceiveMessage<TPayload>(CancellationToken cancellationToken = default(CancellationToken)) where TPayload : Payload
        {
            using (var listener = new NetworkPeerListener(this))
            {
                return listener.ReceivePayload<TPayload>(cancellationToken);
            }
        }

        public void VersionHandshake(CancellationToken cancellationToken = default(CancellationToken))
        {
            VersionHandshake(null, cancellationToken);
        }

        public void VersionHandshake(NodeRequirement requirements, CancellationToken cancellationToken = default(CancellationToken))
        {
            requirements = requirements ?? new NodeRequirement();
            using (NetworkPeerListener listener = CreateListener().Where(p => (p.Message.Payload is VersionPayload)
                || (p.Message.Payload is RejectPayload)
                || (p.Message.Payload is VerAckPayload)))
            {
                this.SendMessageAsync(this.MyVersion);
                Payload payload = listener.ReceivePayload<Payload>(cancellationToken);
                if (payload is RejectPayload)
                {
                    throw new ProtocolException("Handshake rejected : " + ((RejectPayload)payload).Reason);
                }

                var version = (VersionPayload)payload;
                this.PeerVersion = version;
                if (!version.AddressReceiver.Address.Equals(this.MyVersion.AddressFrom.Address))
                {
                    NodeServerTrace.Warning("Different external address detected by the node " + version.AddressReceiver.Address + " instead of " + this.MyVersion.AddressFrom.Address);
                }

                if (version.Version < ProtocolVersion.MIN_PEER_PROTO_VERSION)
                {
                    NodeServerTrace.Warning("Outdated version " + version.Version + " disconnecting");
                    this.Disconnect("Outdated version");
                    return;
                }

                if (!requirements.Check(version))
                {
                    this.Disconnect("The peer does not support the required services requirement");
                    return;
                }

                this.SendMessageAsync(new VerAckPayload());
                listener.ReceivePayload<VerAckPayload>(cancellationToken);
                this.State = NetworkPeerState.HandShaked;
                if (this.Advertize && this.MyVersion.AddressFrom.Address.IsRoutable(true))
                {
                    this.SendMessageAsync(new AddrPayload(new NetworkAddress(this.MyVersion.AddressFrom)
                    {
                        Time = DateTimeOffset.UtcNow
                    }));
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="cancellation"></param>
        public void RespondToHandShake(CancellationToken cancellation = default(CancellationToken))
        {
            using (this.TraceCorrelation.Open())
            {
                using (NetworkPeerListener list = CreateListener().Where(m => (m.Message.Payload is VerAckPayload) || (m.Message.Payload is RejectPayload)))
                {
                    NodeServerTrace.Information("Responding to handshake");
                    this.SendMessageAsync(this.MyVersion);
                    IncomingMessage message = list.ReceiveMessage(cancellation);

                    if (message.Message.Payload is RejectPayload reject)
                        throw new ProtocolException("Version rejected " + reject.Code + " : " + reject.Reason);

                    this.SendMessageAsync(new VerAckPayload());
                    this.State = NetworkPeerState.HandShaked;
                }
            }
        }

        /// <summary>
        /// Disconnects the node and checks the listener thread.
        /// </summary>
        public void Disconnect(string reason = null)
        {
            if (this.IsConnected == false)
                return;

            this.DisconnectNode(reason, null);

            try
            {
                this.AssertNoListeningThread();
                this.connection.Disconnected.WaitOne();
            }
            finally
            {
                this.connection.CleanUp();
            }
        }

        /// <summary>
        /// Disconnects the node without checking the listener thread.
        /// </summary>
        public void DisconnectAsync(string reason = null, Exception exception = null)
        {
            if (this.IsConnected == false)
                return;

            this.DisconnectNode(reason, exception);
            this.connection.CleanUp();
        }

        private void DisconnectNode(string reason = null, Exception exception = null)
        {
            if (Interlocked.CompareExchange(ref this.disconnecting, 1, 0) == 1)
                return;

            using (this.TraceCorrelation.Open())
            {
                NodeServerTrace.Information("Disconnection request " + reason);
                this.State = NetworkPeerState.Disconnecting;
                this.connection.Cancel.Cancel();

                if (this.DisconnectReason == null)
                {
                    this.DisconnectReason = new NodeDisconnectReason()
                    {
                        Reason = reason,
                        Exception = exception
                    };
                }
            }
        }

        private void AssertNoListeningThread()
        {
            if (this.connection.ListenerThreadId == Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Using Disconnect on this thread would result in a deadlock, use DisconnectAsync instead");
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", this.State, this.PeerAddress.Endpoint);
        }


        /// <summary>
        /// Get the chain of headers from the peer (thread safe).
        /// </summary>
        /// <param name="hashStop">The highest block wanted.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The chain of headers.</returns>
        public ConcurrentChain GetChain(uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ConcurrentChain chain = new ConcurrentChain(this.Network);
            this.SynchronizeChain(chain, hashStop, cancellationToken);
            return chain;
        }

        public IEnumerable<ChainedBlock> GetHeadersFromFork(ChainedBlock currentTip, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.AssertState(NetworkPeerState.HandShaked, cancellationToken);

            NodeServerTrace.Information("Building chain");
            using (NetworkPeerListener listener = this.CreateListener().OfType<HeadersPayload>())
            {
                int acceptMaxReorgDepth = 0;
                while (true)
                {
                    // Get before last so, at the end, we should only receive 1 header equals to this one (so we will not have race problems with concurrent GetChains).
                    BlockLocator awaited = currentTip.Previous == null ? currentTip.GetLocator() : currentTip.Previous.GetLocator();
                    SendMessageAsync(new GetHeadersPayload()
                    {
                        BlockLocators = awaited,
                        HashStop = hashStop
                    });

                    while (true)
                    {
                        bool isOurs = false;
                        HeadersPayload headers = null;

                        using (var headersCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            headersCancel.CancelAfter(this.pollHeaderDelay);
                            try
                            {
                                headers = listener.ReceivePayload<HeadersPayload>(headersCancel.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                acceptMaxReorgDepth += 6;
                                if (cancellationToken.IsCancellationRequested)
                                    throw;

                                // Send a new GetHeaders.
                                break;
                            }
                        }

                        // In the special case where the remote node is at height 0 as well as us, then the headers count will be 0.
                        if ((headers.Headers.Count == 0) && (this.PeerVersion.StartHeight == 0) && (currentTip.HashBlock == this.Network.GenesisHash))
                            yield break;

                        if ((headers.Headers.Count == 1) && (headers.Headers[0].GetHash() == currentTip.HashBlock))
                            yield break;

                        foreach (BlockHeader header in headers.Headers)
                        {
                            uint256 hash = header.GetHash();
                            if (hash == currentTip.HashBlock)
                                continue;

                            // The previous headers request timeout, this can arrive in case of big reorg.
                            if (header.HashPrevBlock != currentTip.HashBlock)
                            {
                                int reorgDepth = 0;
                                ChainedBlock tempCurrentTip = currentTip;
                                while (reorgDepth != acceptMaxReorgDepth && tempCurrentTip != null && header.HashPrevBlock != tempCurrentTip.HashBlock)
                                {
                                    reorgDepth++;
                                    tempCurrentTip = tempCurrentTip.Previous;
                                }

                                if (reorgDepth != acceptMaxReorgDepth && tempCurrentTip != null)
                                    currentTip = tempCurrentTip;
                            }

                            if (header.HashPrevBlock == currentTip.HashBlock)
                            {
                                isOurs = true;
                                currentTip = new ChainedBlock(header, hash, currentTip);
                                yield return currentTip;
                                if (currentTip.HashBlock == hashStop)
                                    yield break;
                            }
                            else break; // Not our headers, continue receive.
                        }

                        if (isOurs)
                            break;  //Go ask for next header.
                    }
                }
            }
        }


        /// <summary>
        /// Synchronize a given Chain to the tip of this node if its height is higher. (Thread safe).
        /// </summary>
        /// <param name="chain">The chain to synchronize.</param>
        /// <param name="hashStop">The location until which it synchronize.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IEnumerable<ChainedBlock> SynchronizeChain(ChainBase chain, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ChainedBlock oldTip = chain.Tip;
            List<ChainedBlock> headers = this.GetHeadersFromFork(oldTip, hashStop, cancellationToken).ToList();
            if (headers.Count == 0)
                return new ChainedBlock[0];

            ChainedBlock newTip = headers[headers.Count - 1];

            if (newTip.Height <= oldTip.Height)
                throw new ProtocolException("No tip should have been recieved older than the local one");

            foreach (ChainedBlock header in headers)
            {
                if (!header.Validate(this.Network))
                {
                    throw new ProtocolException("An header which does not pass proof of work verification has been received");
                }
            }

            chain.SetTip(newTip);
            return headers;
        }

        public IEnumerable<Block> GetBlocks(uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var genesis = new ChainedBlock(this.Network.GetGenesis().Header, 0);
            return this.GetBlocksFromFork(genesis, hashStop, cancellationToken);
        }


        public IEnumerable<Block> GetBlocksFromFork(ChainedBlock currentTip, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (NetworkPeerListener listener = CreateListener())
            {
                this.SendMessageAsync(new GetBlocksPayload()
                {
                    BlockLocators = currentTip.GetLocator(),
                });

                IEnumerable<ChainedBlock> headers = this.GetHeadersFromFork(currentTip, hashStop, cancellationToken);

                foreach (Block block in GetBlocks(headers.Select(b => b.HashBlock), cancellationToken))
                {
                    yield return block;
                }
            }
        }

        public IEnumerable<Block> GetBlocks(IEnumerable<ChainedBlock> blocks, CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetBlocks(blocks.Select(c => c.HashBlock), cancellationToken);
        }

        public IEnumerable<Block> GetBlocks(IEnumerable<uint256> neededBlocks, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.AssertState(NetworkPeerState.HandShaked, cancellationToken);

            int simultaneous = 70;
            using (NetworkPeerListener listener = this.CreateListener().OfType<BlockPayload>())
            {
                foreach (List<InventoryVector> invs in neededBlocks.Select(b => new InventoryVector()
                {
                    Type = this.AddSupportedOptions(InventoryType.MSG_BLOCK),
                    Hash = b
                }).Partition(() => simultaneous))
                {
                    var remaining = new Queue<uint256>(invs.Select(k => k.Hash));
                    this.SendMessageAsync(new GetDataPayload(invs.ToArray()));

                    int maxQueued = 0;
                    while (remaining.Count != 0)
                    {
                        Block block = listener.ReceivePayload<BlockPayload>(cancellationToken).Obj;
                        maxQueued = Math.Max(listener.MessageQueue.Count, maxQueued);
                        if (remaining.Peek() == block.GetHash())
                        {
                            remaining.Dequeue();
                            yield return block;
                        }
                    }

                    if (maxQueued < 10) simultaneous *= 2;
                    else simultaneous /= 2;

                    simultaneous = Math.Max(10, simultaneous);
                    simultaneous = Math.Min(10000, simultaneous);
                }
            }
        }

        /// <summary>
        /// Create a listener that will queue messages until disposed.
        /// </summary>
        /// <returns>The listener.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown if used on the listener's thread, as it would result in a deadlock.</exception>
        public NetworkPeerListener CreateListener()
        {
            this.AssertNoListeningThread();
            return new NetworkPeerListener(this);
        }

        private void AssertState(NetworkPeerState nodeState, CancellationToken cancellationToken = default(CancellationToken))
        {
            if ((nodeState == NetworkPeerState.HandShaked) && (this.State == NetworkPeerState.Connected))
                this.VersionHandshake(cancellationToken);

            if (nodeState != this.State)
                throw new InvalidOperationException("Invalid Node state, needed=" + nodeState + ", current= " + this.State);
        }

        public uint256[] GetMempool(CancellationToken cancellationToken = default(CancellationToken))
        {
            AssertState(NetworkPeerState.HandShaked);
            using (NetworkPeerListener listener = this.CreateListener().OfType<InvPayload>())
            {
                this.SendMessageAsync(new MempoolPayload());

                List<uint256> invs = listener.ReceivePayload<InvPayload>(cancellationToken).Inventory.Select(i => i.Hash).ToList();
                List<uint256> result = invs;
                while (invs.Count == InvPayload.MaxInventorySize)
                {
                    invs = listener.ReceivePayload<InvPayload>(cancellationToken).Inventory.Select(i => i.Hash).ToList();
                    result.AddRange(invs);
                }

                return result.ToArray();
            }
        }

        /// <summary>
        /// Retrieve transactions from the mempool.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Transactions in the mempool.</returns>
        public Transaction[] GetMempoolTransactions(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetMempoolTransactions(GetMempool(), cancellationToken);
        }

        /// <summary>
        /// Retrieve transactions from the mempool by ids.
        /// </summary>
        /// <param name="txIds">Transaction ids to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The transactions, if a transaction is not found, then it is not returned in the array.</returns>
        public Transaction[] GetMempoolTransactions(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.AssertState(NetworkPeerState.HandShaked);

            if (txIds.Length == 0)
                return new Transaction[0];

            List<Transaction> result = new List<Transaction>();

            using (NetworkPeerListener listener = CreateListener().Where(m => (m.Message.Payload is TxPayload) || (m.Message.Payload is NotFoundPayload)))
            {
                foreach (List<uint256> batch in txIds.Partition(500))
                {
                    this.SendMessageAsync(new GetDataPayload(batch.Select(txid => new InventoryVector()
                    {
                        Type = this.AddSupportedOptions(InventoryType.MSG_TX),
                        Hash = txid
                    }).ToArray()));

                    try
                    {
                        List<Transaction> batchResult = new List<Transaction>();
                        while (batchResult.Count < batch.Count)
                        {
                            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10.0)))
                            {
                                using (var receiveTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
                                {
                                    Payload payload = listener.ReceivePayload<Payload>(receiveTimeout.Token);
                                    if (payload is NotFoundPayload)
                                        batchResult.Add(null);
                                    else
                                        batchResult.Add(((TxPayload)payload).Obj);
                                }
                            }
                        }

                        result.AddRange(batchResult);
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                    }
                }
            }

            return result.Where(r => r != null).ToArray();
        }

        /// <summary>
        /// Add supported option to the input inventory type
        /// </summary>
        /// <param name="inventoryType">Inventory type (like MSG_TX)</param>
        /// <returns>Inventory type with options (MSG_TX | MSG_WITNESS_FLAG)</returns>
        public InventoryType AddSupportedOptions(InventoryType inventoryType)
        {
            if ((this.ActualTransactionOptions & TransactionOptions.Witness) != 0)
                inventoryType |= InventoryType.MSG_WITNESS_FLAG;

            return inventoryType;
        }

        public void Dispose()
        {
            Disconnect("Node disposed");
        }

        /// <summary>
        /// Emit a ping and wait the pong.
        /// </summary>
        /// <param name="cancellation"></param>
        /// <returns>Latency.</returns>
        public TimeSpan PingPong(CancellationToken cancellation = default(CancellationToken))
        {
            using (NetworkPeerListener listener = CreateListener().OfType<PongPayload>())
            {
                var ping = new PingPayload()
                {
                    Nonce = RandomUtils.GetUInt64()
                };

                DateTimeOffset before = DateTimeOffset.UtcNow;
                SendMessageAsync(ping);

                while (listener.ReceivePayload<PongPayload>(cancellation).Nonce != ping.Nonce)
                {
                }

                DateTimeOffset after = DateTimeOffset.UtcNow;
                return after - before;
            }
        }
    }
}