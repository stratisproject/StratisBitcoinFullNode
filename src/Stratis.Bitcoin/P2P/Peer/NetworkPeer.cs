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
    /// <summary>
    /// State of the network connection to a peer.
    /// </summary>
    public enum NetworkPeerState : int
    {
        /// <summary>An error occurred during a network operation.</summary>
        Failed,

        /// <summary>Shutdown has been initiated, the node went offline.</summary>
        Offline,

        /// <summary>Process of disconnecting the peer has been initiated.</summary>
        Disconnecting,

        /// <summary>Network connection between with the peer has been established.</summary>
        Connected,

        /// <summary>The node and the peer exchanged version information.</summary>
        HandShaked
    }

    /// <summary>
    /// Explanation of why a peer was disconnected.
    /// </summary>
    public class NetworkPeerDisconnectReason
    {
        /// <summary>Human readable reason for disconnecting.</summary>
        public string Reason { get; set; }

        /// <summary>Exception because of which the disconnection happened, or <c>null</c> if there were no exception.</summary>
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// Protocol requirement for network peers the node wants to be connected to.
    /// </summary>
    public class NetworkPeerRequirement
    {
        /// <summary>Minimal protocol version that the peer must support or <c>null</c> if there is no requirement for minimal protocol version.</summary>
        public ProtocolVersion? MinVersion { get; set; }

        /// <summary>Specification of network services that the peer must provide.</summary>
        public NetworkPeerServices RequiredServices { get; set; }

        /// <summary><c>true</c> to require the peer to support SPV, <c>false</c> otherwise..</summary>
        public bool SupportSPV { get; set; }

        /// <summary>
        /// Checks a version payload from a peer against the requirements.
        /// </summary>
        /// <param name="version">Version payload to check.</param>
        /// <returns><c>true</c> if the version payload satisfies the protocol requirements, <c>false</c> otherwise.</returns>
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

    /// <summary>
    /// Type of event handler that is triggered on network peer disconnection.
    /// </summary>
    /// <param name="peer">Network peer that was disconnected.</param>
    public delegate void NetworkPeerDisconnectedEventHandler(NetworkPeer peer);

    /// <summary>
    /// Type of event handler that is triggered when a new message is received from a network peer.
    /// </summary>
    /// <param name="peer">Network peer from which the message was received.</param>
    /// <param name="message">Message that was received.</param>
    public delegate void NetworkPeerMessageReceivedEventHandler(NetworkPeer peer, IncomingMessage message);

    /// <summary>
    /// Type of event handler that is triggered when the network state of a peer was changed.
    /// </summary>
    /// <param name="peer">Network peer which network state was changed.</param>
    /// <param name="oldState">Previous network state of the peer.</param>
    public delegate void NetworkPeerStateChangedEventHandler(NetworkPeer peer, NetworkPeerState oldState);

    /// <summary>Information to a message that the node sent to a peer.</summary>
    public class SentMessage
    {
        /// <summary>Payload of the sent message.</summary>
        public Payload Payload;
    }

    /// <summary>
    /// Represents a network connection to a peer. It is responsible for reading incoming messages form the peer 
    /// and sending messages from the node to the peer.
    /// </summary>
    public class NetworkPeerConnection
    {
        /// <summary>Logger for the node.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Network peer that this object represents the node's connection to.</summary>
        public NetworkPeer Peer { get; private set; }

        /// <summary>Connected network socket to the peer.</summary>
        public Socket Socket { get; private set; }

        /// <summary>Event that is set when the connection is closed.</summary>
        public ManualResetEvent Disconnected { get; private set; }

        /// <summary>Cancellation to be triggered at shutdown to abort all pending operations on the connection.</summary>
        public CancellationTokenSource Cancel { get; private set; }

        /// <summary>Queue of messages to be sent to a peer over the network connection.</summary>
        internal BlockingCollection<SentMessage> Messages;

        /// <summary>Set to <c>1</c> when a cleanup has been initiated, otherwise <c>0</c>.</summary>
        private int cleaningUp;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="peer">Network peer the node is connection to.</param>
        /// <param name="socket">Connected network socket to the peer.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public NetworkPeerConnection(NetworkPeer peer, Socket socket, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{peer.PeerAddress.Endpoint}] ");
            this.dateTimeProvider = dateTimeProvider;

            this.Peer = peer;
            this.Socket = socket;
            this.Messages = new BlockingCollection<SentMessage>(new ConcurrentQueue<SentMessage>());
        }

        /// <summary>
        /// Starts two threads, one is responsible for receiving incoming message from the peer 
        /// and the other is responsible for sending node's message, which are waiting in a queue, to the peer.
        /// </summary>
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

                                    processing = null;
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("Sending cancelled.");
                }
                catch (Exception ex)
                {
                    this.logger.LogTrace("Exception occurred: '{0}'", ex.ToString());
                    unhandledException = ex;
                }

                if (processing != null)
                    this.Messages.Add(processing);

                foreach (SentMessage pending in this.Messages)
                {
                    this.logger.LogTrace("Connection terminated before message '{0}' could be sent.", pending.Payload?.Command);
                }

                this.Messages = new BlockingCollection<SentMessage>(new ConcurrentQueue<SentMessage>());

                this.logger.LogDebug("Terminating sending thread.");
                this.EndListen(unhandledException);

                this.logger.LogTrace("(-)");
            }).Start();

            new Thread(() =>
            {
                this.logger.LogTrace("()");

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

                            this.Peer.LastSeen = this.dateTimeProvider.GetUtcNow();
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

        /// <summary>
        /// When the connection is terminated, this method cleans up and informs connected behaviors about the termination.
        /// </summary>
        /// <param name="unhandledException">Error exception explaining why the termination occurred, or <c>null</c> if the connection was closed gracefully.</param>
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

        /// <summary>
        /// Disposes resources used by the object.
        /// </summary>
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

        /// <summary>Time in UTC when the connection to the peer was established.</summary>
        public DateTime ConnectedAt { get; private set; }

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

        /// <summary>Last time in UTC the node received something from this peer.</summary>
        public DateTime LastSeen { get; set; }

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
        public NetworkOptions PreferredTransactionOptions { get; private set; } = NetworkOptions.All;

        /// <summary>Transaction options supported by the peer.</summary>
        public NetworkOptions SupportedTransactionOptions { get; private set; } = NetworkOptions.None;

        /// <summary>Transaction options we prefer and which is also supported by peer.</summary>
        public NetworkOptions ActualTransactionOptions
        {
            get
            {
                return this.PreferredTransactionOptions & this.SupportedTransactionOptions;
            }
        }

        /// <summary>When a peer is disconnected this is set to human readable information about why it happened.</summary>
        public NetworkPeerDisconnectReason DisconnectReason { get; set; }

        private Socket Socket
        {
            get
            {
                return this.connection.Socket;
            }
        }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; set; }

        public event NetworkPeerStateChangedEventHandler StateChanged;
        public event NetworkPeerMessageReceivedEventHandler MessageReceived;
        public event NetworkPeerDisconnectedEventHandler Disconnected;

        /// <summary>
        /// Dummy constructor for testing only.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Provider of time functions.</param>
        /// <remarks>TODO: Remove this constructor as soon as we can mock the node in tests.</remarks>
        public NetworkPeer(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

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
            this.MyVersion = parameters.CreateVersion(peerAddress.Endpoint, network, this.dateTimeProvider.GetTimeOffset());
            this.Network = network;
            this.PeerAddress = peerAddress;
            this.LastSeen = peerAddress.Time.UtcDateTime;

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
                        this.ConnectedAt = this.dateTimeProvider.GetUtcNow();

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
            this.MyVersion = parameters.CreateVersion(peerAddress.Endpoint, network, this.dateTimeProvider.GetTimeOffset());
            this.Network = network;
            this.PeerAddress = peerAddress;
            this.connection = new NetworkPeerConnection(this, socket, this.dateTimeProvider, this.loggerFactory);
            this.PeerVersion = peerVersion;
            this.LastSeen = peerAddress.Time.UtcDateTime;
            this.ConnectedAt = this.dateTimeProvider.GetUtcNow();

            this.logger.LogTrace("Connected to advertised node '{0}'.", this.PeerAddress.Endpoint);
            this.State = NetworkPeerState.Connected;

            this.InitDefaultBehaviors(parameters);
            this.connection.BeginListen();

            this.logger.LogTrace("(-)");
        }

        private void OnStateChanged(NetworkPeerState previous)
        {
            this.logger.LogTrace("({0}:{1})", nameof(previous), previous);

            NetworkPeerStateChangedEventHandler stateChanged = StateChanged;
            if (stateChanged != null)
            {
                foreach (NetworkPeerStateChangedEventHandler handler in stateChanged.GetInvocationList().Cast<NetworkPeerStateChangedEventHandler>())
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
                this.TimeOffset = this.dateTimeProvider.GetTimeOffset() - version.Timestamp;
                if ((version.Services & NetworkPeerServices.NODE_WITNESS) != 0)
                    this.SupportedTransactionOptions |= NetworkOptions.Witness;
            }

            if (message.Message.Payload is HaveWitnessPayload)
                this.SupportedTransactionOptions |= NetworkOptions.Witness;

            var last = new ActionFilter((m, n) =>
            {
                this.MessageProducer.PushMessage(m);
                NetworkPeerMessageReceivedEventHandler messageReceived = MessageReceived;
                if (messageReceived != null)
                {
                    foreach (NetworkPeerMessageReceivedEventHandler handler in messageReceived.GetInvocationList().Cast<NetworkPeerMessageReceivedEventHandler>())
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
                    this.logger.LogError("Exception occurred: {0}", ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString());
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
                    this.logger.LogError("Exception occurred: {0}", ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString());
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void OnDisconnected()
        {
            this.logger.LogTrace("()");

            NetworkPeerDisconnectedEventHandler disconnected = Disconnected;
            if (disconnected != null)
            {
                foreach (NetworkPeerDisconnectedEventHandler handler in disconnected.GetInvocationList().Cast<NetworkPeerDisconnectedEventHandler>())
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

            this.VersionHandshake(null, cancellationToken);

            this.logger.LogTrace("(-)");
        }

        public void VersionHandshake(NetworkPeerRequirement requirements, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(requirements), nameof(requirements.RequiredServices), requirements?.RequiredServices);

            requirements = requirements ?? new NetworkPeerRequirement();
            using (NetworkPeerListener listener = this.CreateListener().Where(p => (p.Message.Payload is VersionPayload)
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
                        Time = this.dateTimeProvider.GetTimeOffset()
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
                this.logger.LogTrace("(-)[DISCONNECTING");
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

        public override string ToString()
        {
            return string.Format("{0} ({1})", this.State, this.PeerAddress.Endpoint);
        }

        /// <summary>
        /// Create a listener that will queue messages until disposed.
        /// </summary>
        /// <returns>The listener.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown if used on the listener's thread, as it would result in a deadlock.</exception>
        public NetworkPeerListener CreateListener()
        {
            return new NetworkPeerListener(this);
        }

        /// <summary>
        /// Add supported option to the input inventory type
        /// </summary>
        /// <param name="inventoryType">Inventory type (like MSG_TX)</param>
        /// <returns>Inventory type with options (MSG_TX | MSG_WITNESS_FLAG)</returns>
        public InventoryType AddSupportedOptions(InventoryType inventoryType)
        {
            if ((this.ActualTransactionOptions & NetworkOptions.Witness) != 0)
                inventoryType |= InventoryType.MSG_WITNESS_FLAG;

            return inventoryType;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Disconnect("Node disposed");
        }
    }
}