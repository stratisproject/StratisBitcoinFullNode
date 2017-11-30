using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
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

    public class NetworkPeerDisconnectReason
    {
        public string Reason { get; set; }
        public Exception Exception { get; set; }
    }

    public class NetworkPeerRequirement
    {
        public ProtocolVersion? MinVersion { get; set; }
        public NetworkPeerServices RequiredServices { get; set; }

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

                if ((ProtocolVersion.NO_BLOOM_VERSION <= version.Version) && ((version.Services & NetworkPeerServices.NODE_BLOOM) == 0))
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
        /// <summary>Logger for the node.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public NetworkPeer Peer { get; private set; }

        public Socket Socket { get; private set; }

        public ManualResetEvent Disconnected { get; private set; }

        public CancellationTokenSource Cancel { get; private set; }

        internal BlockingCollection<SentMessage> Messages;

        private int cleaningUp;
        public int ListenerThreadId;

        public NetworkPeerConnection(NetworkPeer peer, Socket socket, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{peer.PeerAddress.Endpoint}] ");
            this.dateTimeProvider = dateTimeProvider;

            this.Peer = peer;
            this.Socket = socket;
            this.Messages = new BlockingCollection<SentMessage>(new ConcurrentQueue<SentMessage>());
        }

        public void BeginListen()
        {
            this.logger.LogTrace("()");

            this.Disconnected = new ManualResetEvent(false);
            this.Cancel = new CancellationTokenSource();

            new Thread(() =>
            {
                this.logger.LogTrace("()");
                SentMessage processing = null;
                Exception unhandledException = null;

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
                                    Magic = this.Peer.Network.Magic,
                                    Payload = payload
                                };

                                this.logger.LogTrace("Sending message: '{0}'", message);

                                using (MemoryStream ms = new MemoryStream())
                                {
                                    message.ReadWrite(new BitcoinStream(ms, true)
                                    {
                                        ProtocolVersion = this.Peer.Version,
                                        TransactionOptions = this.Peer.SupportedTransactionOptions
                                    });

                                    byte[] bytes = ms.ToArrayEfficient();

                                    socketEventManager.SocketEvent.SetBuffer(bytes, 0, bytes.Length);
                                    this.Peer.Counter.AddWritten(bytes.Length);
                                }

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
                    this.logger.LogTrace("Connection terminated before message '{0}' could be sent.", pending.Payload?.Command);
                    pending.Completion.SetException(new OperationCanceledException("The peer has been disconnected"));
                }

                this.Messages = new BlockingCollection<SentMessage>(new ConcurrentQueue<SentMessage>());

                this.logger.LogDebug("Terminating sending thread.");
                this.EndListen(unhandledException);

                this.logger.LogTrace("(-)");
            }).Start();

            new Thread(() =>
            {
                this.logger.LogTrace("()");

                this.ListenerThreadId = Thread.CurrentThread.ManagedThreadId;

                this.logger.LogTrace("Start listenting.");
                Exception unhandledException = null;
                byte[] buffer = this.Peer.ReuseBuffer ? new byte[1024 * 1024] : null;
                try
                {
                    using (var stream = new NetworkStream(this.Socket, false))
                    {
                        while (!this.Cancel.Token.IsCancellationRequested)
                        {
                            PerformanceCounter counter;

                            Message message = Message.ReadNext(stream, this.Peer.Network, this.Peer.Version, this.Cancel.Token, buffer, out counter);

                            this.logger.LogTrace("Receiving message: '{0}'", message);

                            this.Peer.LastSeen = DateTimeOffset.UtcNow;
                            this.Peer.Counter.Add(counter);
                            this.Peer.OnMessageReceived(new IncomingMessage()
                            {
                                Message = message,
                                Socket = this.Socket,
                                Length = counter.ReadBytes,
                                NetworkPeer = this.Peer
                            });
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("Listening cancelled.");
                }
                catch (Exception ex)
                {
                    this.logger.LogTrace("Exception occurred: {0}", ex);
                    unhandledException = ex;
                }

                this.logger.LogDebug("Terminating listening thread.");
                this.EndListen(unhandledException);

                this.logger.LogTrace("(-)");
            }).Start();

            this.logger.LogTrace("(-)");
        }

        private void EndListen(Exception unhandledException)
        {
            this.logger.LogTrace("()");

            if (Interlocked.CompareExchange(ref this.cleaningUp, 1, 0) == 1)
            {
                this.logger.LogTrace("(-)[CLEANING_UP]");
                return;
            }

            if (!this.Cancel.IsCancellationRequested)
            {
                this.logger.LogDebug("Connection to server stopped unexpectedly, error message '{0}'.", unhandledException.Message);

                this.Peer.DisconnectReason = new NetworkPeerDisconnectReason()
                {
                    Reason = "Unexpected exception while connecting to socket",
                    Exception = unhandledException
                };
                this.Peer.State = NetworkPeerState.Failed;
            }

            if (this.Peer.State != NetworkPeerState.Failed)
                this.Peer.State = NetworkPeerState.Offline;

            if (this.Cancel.IsCancellationRequested == false)
                this.Cancel.Cancel();

            if (this.Disconnected.GetSafeWaitHandle().IsClosed == false)
                this.Disconnected.Set();

            Utils.SafeCloseSocket(this.Socket);

            foreach (INetworkPeerBehavior behavior in this.Peer.Behaviors)
            {
                try
                {
                    behavior.Detach();
                }
                catch (Exception ex)
                {
                    this.logger.LogError("Error while detaching behavior '{0}': {1}", behavior.GetType().FullName, ex.ToString());
                }
            }
        }

        internal void CleanUp()
        {
            this.logger.LogTrace("()");

            this.Disconnected.Dispose();
            this.Cancel.Dispose();

            this.logger.LogTrace("(-)");
        }
    }

    public class NetworkPeer : IDisposable
    {
        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

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
                this.logger.LogTrace("State changed from {0} to {1}.", this.state, value);
                NetworkPeerState previous = this.state;
                this.state = value;
                if (previous != this.state)
                {
                    this.OnStateChanged(previous);
                    if ((value == NetworkPeerState.Failed) || (value == NetworkPeerState.Offline))
                    {
                        this.logger.LogTrace("Communication closed.");
                        this.OnDisconnected();
                    }
                }
            }
        }

        public IPAddress RemoteSocketAddress { get; private set; }
        public IPEndPoint RemoteSocketEndpoint { get; private set; }
        public int RemoteSocketPort { get; private set; }
        public bool Inbound { get; private set; }

        public bool ReuseBuffer { get; private set; }
        public NetworkPeerBehaviorsCollection Behaviors { get; private set; }
        public NetworkAddress PeerAddress { get; private set; }

        public DateTimeOffset LastSeen { get; set; }

        public TimeSpan? TimeOffset { get; private set; }

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
        public NetworkPeerFiltersCollection Filters { get; private set; } = new NetworkPeerFiltersCollection();

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

        public NetworkPeerDisconnectReason DisconnectReason { get; set; }

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

        public NetworkPeer(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            // This constructor is used for testing until the Node class has an interface and can be mocked.
            this.Behaviors = new NetworkPeerBehaviorsCollection(this);
        }

        public NetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{peerAddress.Endpoint}] ");

            this.logger.LogTrace("()");
            this.dateTimeProvider = dateTimeProvider;

            parameters = parameters ?? new NetworkPeerConnectionParameters();
            this.Inbound = false;
            this.Behaviors = new NetworkPeerBehaviorsCollection(this);
            this.MyVersion = parameters.CreateVersion(peerAddress.Endpoint, network);
            this.Network = network;
            this.PeerAddress = peerAddress;
            this.LastSeen = peerAddress.Time;

            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

            this.connection = new NetworkPeerConnection(this, socket, this.dateTimeProvider, this.loggerFactory);

            socket.ReceiveBufferSize = parameters.ReceiveBufferSize;
            socket.SendBufferSize = parameters.SendBufferSize;
            try
            {
                using (var completedEvent = new ManualResetEvent(false))
                {
                    using (var nodeSocketEventManager = NodeSocketEventManager.Create(completedEvent, peerAddress.Endpoint))
                    {
                        this.logger.LogTrace("Connecting to '{0}'.", peerAddress.Endpoint);

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

                        this.logger.LogTrace("Outbound connection to '{0}' established.", peerAddress.Endpoint);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("Connection to '{0}' cancelled.", peerAddress.Endpoint);
                Utils.SafeCloseSocket(socket);
                this.State = NetworkPeerState.Offline;

                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                Utils.SafeCloseSocket(socket);
                this.DisconnectReason = new NetworkPeerDisconnectReason()
                {
                    Reason = "Unexpected exception while connecting to socket",
                    Exception = ex
                };

                this.State = NetworkPeerState.Failed;

                throw;
            }

            this.InitDefaultBehaviors(parameters);
            this.connection.BeginListen();

            this.logger.LogTrace("(-)");
        }

        public NetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, Socket socket, VersionPayload peerVersion, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.RemoteSocketEndpoint = ((IPEndPoint)socket.RemoteEndPoint);
            this.RemoteSocketAddress = ((IPEndPoint)socket.RemoteEndPoint).Address;
            this.RemoteSocketPort = ((IPEndPoint)socket.RemoteEndPoint).Port;

            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.RemoteSocketEndpoint}] ");

            this.logger.LogTrace("()");

            this.dateTimeProvider = dateTimeProvider;

            this.Inbound = true;
            this.Behaviors = new NetworkPeerBehaviorsCollection(this);
            this.MyVersion = parameters.CreateVersion(peerAddress.Endpoint, network);
            this.Network = network;
            this.PeerAddress = peerAddress;
            this.connection = new NetworkPeerConnection(this, socket, this.dateTimeProvider, this.loggerFactory);
            this.PeerVersion = peerVersion;
            this.LastSeen = peerAddress.Time;
            this.ConnectedAt = DateTimeOffset.UtcNow;

            this.logger.LogTrace("Connected to advertised node '{0}'.", this.PeerAddress.Endpoint);
            this.State = NetworkPeerState.Connected;

            this.InitDefaultBehaviors(parameters);
            this.connection.BeginListen();

            this.logger.LogTrace("(-)");
        }

        private void OnStateChanged(NetworkPeerState previous)
        {
            this.logger.LogTrace("({0}:{1})", nameof(previous), previous);

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
                        this.logger.LogError("Exception occurred: {0}", ex.InnerException.ToString());
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        public void OnMessageReceived(IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(message), message.Message.Command);

            var version = message.Message.Payload as VersionPayload;
            if ((version != null) && (this.State == NetworkPeerState.HandShaked))
            {
                if (message.NetworkPeer.Version >= ProtocolVersion.REJECT_VERSION)
                    message.NetworkPeer.SendMessageAsync(new RejectPayload()
                    {
                        Code = RejectCode.DUPLICATE
                    });
            }

            if (version != null)
            {
                this.TimeOffset = DateTimeOffset.Now - version.Timestamp;
                if ((version.Services & NetworkPeerServices.NODE_WITNESS) != 0)
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
                            this.logger.LogError("Exception occurred: {0}", ex.InnerException.ToString());
                        }
                    }
                }
            });

            IEnumerator<INetworkPeerFilter> enumerator = this.Filters.Concat(new[] { last }).GetEnumerator();
            this.FireFilters(enumerator, message);

            this.logger.LogTrace("(-)");
        }

        private void OnSendingMessage(Payload payload, Action final)
        {
            this.logger.LogTrace("({0}:{1})", nameof(payload), payload);

            IEnumerator<INetworkPeerFilter> enumerator = this.Filters.Concat(new[] { new ActionFilter(null, (n, p, a) => final()) }).GetEnumerator();
            this.FireFilters(enumerator, payload);

            this.logger.LogTrace("(-)");
        }

        private void FireFilters(IEnumerator<INetworkPeerFilter> enumerator, Payload payload)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(payload), payload);

            if (enumerator.MoveNext())
            {
                INetworkPeerFilter filter = enumerator.Current;
                try
                {
                    filter.OnSendingMessage(this, payload, () => FireFilters(enumerator, payload));
                }
                catch (Exception ex)
                {
                    this.logger.LogError("Exception occurred: {0}", ex.InnerException.ToString());
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void FireFilters(IEnumerator<INetworkPeerFilter> enumerator, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(message), message);

            if (enumerator.MoveNext())
            {
                INetworkPeerFilter filter = enumerator.Current;
                try
                {
                    filter.OnReceivingMessage(message, () => FireFilters(enumerator, message));
                }
                catch (Exception ex)
                {
                    this.logger.LogError("Exception occurred: {0}", ex.InnerException.ToString());
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void OnDisconnected()
        {
            this.logger.LogTrace("()");

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
                        this.logger.LogError("Exception occurred: {0}", ex.InnerException.ToString());
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void InitDefaultBehaviors(NetworkPeerConnectionParameters parameters)
        {
            this.logger.LogTrace("()");

            this.Advertize = parameters.Advertize;
            this.PreferredTransactionOptions = parameters.PreferredTransactionOptions;
            this.ReuseBuffer = parameters.ReuseBuffer;

            this.Behaviors.DelayAttach = true;
            foreach (INetworkPeerBehavior behavior in parameters.TemplateBehaviors)
            {
                this.Behaviors.Add(behavior.Clone());
            }

            this.Behaviors.DelayAttach = false;

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Send a message to the peer asynchronously.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <param name="System.OperationCanceledException">The node has been disconnected.</param>
        public Task SendMessageAsync(Payload payload)
        {
            Guard.NotNull(payload, nameof(payload));
            this.logger.LogTrace("({0}:'{1}')", nameof(payload), payload);

            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            if (!this.IsConnected)
            {
                completion.SetException(new OperationCanceledException("The peer has been disconnected"));
                return completion.Task;
            }

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

            this.logger.LogTrace("(-)");
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
            this.logger.LogTrace("({0}:'{1}')", nameof(payload), payload);

            try
            {
                SendMessageAsync(payload).Wait(cancellation);
            }
            catch (AggregateException aex)
            {
                this.logger.LogTrace("Exception occurred: {0}", aex.InnerException.ToString());
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        public TPayload ReceiveMessage<TPayload>(TimeSpan timeout) where TPayload : Payload
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(timeout), timeout);

            using (var source = new CancellationTokenSource())
            {
                source.CancelAfter(timeout);
                TPayload res = ReceiveMessage<TPayload>(source.Token);

                this.logger.LogTrace("(-):'{0}'", res);
                return res;
            }
        }

        public TPayload ReceiveMessage<TPayload>(CancellationToken cancellationToken = default(CancellationToken)) where TPayload : Payload
        {
            this.logger.LogTrace("()");

            using (var listener = new NetworkPeerListener(this))
            {
                TPayload res = listener.ReceivePayload<TPayload>(cancellationToken);

                this.logger.LogTrace("(-):'{0}'", res);
                return res;
            }

        }

        public void VersionHandshake(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            VersionHandshake(null, cancellationToken);

            this.logger.LogTrace("(-)");
        }

        public void VersionHandshake(NetworkPeerRequirement requirements, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(requirements), nameof(requirements.RequiredServices), requirements?.RequiredServices);

            requirements = requirements ?? new NetworkPeerRequirement();
            using (NetworkPeerListener listener = CreateListener().Where(p => (p.Message.Payload is VersionPayload)
                || (p.Message.Payload is RejectPayload)
                || (p.Message.Payload is VerAckPayload)))
            {
                this.SendMessageAsync(this.MyVersion);
                Payload payload = listener.ReceivePayload<Payload>(cancellationToken);
                if (payload is RejectPayload)
                {
                    this.logger.LogTrace("(-)[HANDSHAKE_REJECTED]");
                    throw new ProtocolException("Handshake rejected : " + ((RejectPayload)payload).Reason);
                }

                var version = (VersionPayload)payload;
                this.PeerVersion = version;
                if (!version.AddressReceiver.Address.Equals(this.MyVersion.AddressFrom.Address))
                {
                    this.logger.LogDebug("Different external address detected by the node '{0}' instead of '{1}'.", version.AddressReceiver.Address, this.MyVersion.AddressFrom.Address);
                }

                if (version.Version < ProtocolVersion.MIN_PEER_PROTO_VERSION)
                {
                    this.logger.LogDebug("Outdated version {0} received, disconnecting peer.", version.Version);

                    this.Disconnect("Outdated version");
                    this.logger.LogTrace("(-)[OUTDATED]");
                    return;
                }

                if (!requirements.Check(version))
                {
                    this.logger.LogTrace("(-)[UNSUPPORTED_REQUIREMENTS]");
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

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// </summary>
        /// <param name="cancellation"></param>
        public void RespondToHandShake(CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            using (NetworkPeerListener list = CreateListener().Where(m => (m.Message.Payload is VerAckPayload) || (m.Message.Payload is RejectPayload)))
            {
                this.logger.LogTrace("Responding to handshake.");
                this.SendMessageAsync(this.MyVersion);
                IncomingMessage message = list.ReceiveMessage(cancellation);

                if (message.Message.Payload is RejectPayload reject)
                {
                    this.logger.LogTrace("Version rejected: code {0}, reason {1}.", reject.Code, reject.Reason);
                    this.logger.LogTrace("(-)[VERSION_REJECTED]");
                    throw new ProtocolException("Version rejected " + reject.Code + " : " + reject.Reason);
                }

                this.SendMessageAsync(new VerAckPayload());
                this.State = NetworkPeerState.HandShaked;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Disconnects the node and checks the listener thread.
        /// </summary>
        public void Disconnect(string reason = null)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(reason), reason);

            if (this.IsConnected == false)
            {
                this.logger.LogTrace("(-)[NOT_CONNECTED]");
                return;
            }

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

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Disconnects the node without checking the listener thread.
        /// </summary>
        public void DisconnectAsync(string reason = null, Exception exception = null)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(reason), reason);

            if (this.IsConnected == false)
            {
                this.logger.LogTrace("(-)[NOT_CONNECTED]");
                return;
            }

            this.DisconnectNode(reason, exception);
            this.connection.CleanUp();

            this.logger.LogTrace("(-)");
        }

        private void DisconnectNode(string reason = null, Exception exception = null)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(reason), reason);

            if (Interlocked.CompareExchange(ref this.disconnecting, 1, 0) == 1)
            {
                this.logger.LogTrace("(-)[DISCONNETING");
                return;
            }

            this.State = NetworkPeerState.Disconnecting;
            this.connection.Cancel.Cancel();

            if (this.DisconnectReason == null)
            {
                this.DisconnectReason = new NetworkPeerDisconnectReason()
                {
                    Reason = reason,
                    Exception = exception
                };
            }

            this.logger.LogTrace("(-)");
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
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(currentTip), currentTip, nameof(hashStop), hashStop);

            this.AssertState(NetworkPeerState.HandShaked, cancellationToken);

            this.logger.LogDebug("Building chain.");
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
                        {
                            this.logger.LogTrace("(-)[BREAK_HC_0]");
                            yield break;
                        }

                        if ((headers.Headers.Count == 1) && (headers.Headers[0].GetHash() == currentTip.HashBlock))
                        {
                            this.logger.LogTrace("(-)[BREAK_HC_1]");
                            yield break;
                        }

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

                                this.logger.LogTrace("(-):'{0}'", currentTip);
                                yield return currentTip;

                                this.logger.LogTrace("({0}:'{1}')[CONTINUE]", nameof(currentTip), currentTip);

                                if (currentTip.HashBlock == hashStop)
                                {
                                    this.logger.LogTrace("(-)[BREAK_STOP]");
                                    yield break;
                                }
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
            this.logger.LogTrace("({0}:'{1}')", nameof(hashStop), hashStop);

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
                    this.logger.LogTrace("(-)[BAD_HEADER]");
                    throw new ProtocolException("A header which does not pass proof of work verification has been received");
                }
            }

            chain.SetTip(newTip);

            this.logger.LogTrace("(-):'{0}')", headers);
            return headers;
        }

        public IEnumerable<Block> GetBlocks(uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var genesis = new ChainedBlock(this.Network.GetGenesis().Header, 0);
            return this.GetBlocksFromFork(genesis, hashStop, cancellationToken);
        }


        public IEnumerable<Block> GetBlocksFromFork(ChainedBlock currentTip, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(currentTip), currentTip, nameof(hashStop), hashStop);

            using (NetworkPeerListener listener = CreateListener())
            {
                this.SendMessageAsync(new GetBlocksPayload()
                {
                    BlockLocators = currentTip.GetLocator(),
                });

                IEnumerable<ChainedBlock> headers = this.GetHeadersFromFork(currentTip, hashStop, cancellationToken);

                foreach (Block block in GetBlocks(headers.Select(b => b.HashBlock), cancellationToken))
                {
                    this.logger.LogTrace("(-):'{0}'", block);
                    yield return block;

                    this.logger.LogTrace("({0}:'{1}')[CONTINUE]", nameof(currentTip), currentTip);
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

        /// <inheritdoc />
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
            this.logger.LogTrace("()");

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

                TimeSpan res = after - before;
                this.logger.LogTrace("(-):{0}", res);
                return res;
            }
        }
    }
}