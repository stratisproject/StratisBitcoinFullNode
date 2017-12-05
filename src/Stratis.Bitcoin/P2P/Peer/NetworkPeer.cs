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

        /// <summary>Network connection with the peer has been established.</summary>
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

        /// <summary>Exception because of which the disconnection happened, or <c>null</c> if there were no exceptions.</summary>
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

        /// <summary><c>true</c> to require the peer to support SPV, <c>false</c> otherwise.</summary>
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

    /// <summary>Information about a message that the node sent to a peer.</summary>
    public class SentMessage
    {
        /// <summary>Payload of the sent message.</summary>
        public Payload Payload;

        /// <summary>
        /// Completion of the send message task. 
        /// </summary>
        /// <remarks>
        /// The result of the operation is set to <c>true</c> when the message is successfully sent to the peer.
        /// It is never set to <c>false</c>, it can only fail if the peer is disconnected, in which case an exception is set on the completion.
        /// </remarks>
        public TaskCompletionSource<bool> Completion;
    }

    /// <summary>
    /// Represents a network connection to a peer. It is responsible for reading incoming messages from the peer 
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
        public NetworkPeerClient Client { get; private set; }

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
        /// <param name="peer">Network peer the node is connected to.</param>
        /// <param name="client">Connected network client to the peer.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public NetworkPeerConnection(NetworkPeer peer, NetworkPeerClient client, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{peer.PeerAddress.Endpoint}] ");
            this.dateTimeProvider = dateTimeProvider;

            this.Peer = peer;
            this.Client = client;
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

            // This is sending thread.
            new Thread(() =>
            {
                this.logger.LogTrace("()");
                SentMessage processing = null;
                Exception unhandledException = null;

                try
                {
                    foreach (SentMessage messageToSend in this.Messages.GetConsumingEnumerable(this.Cancel.Token))
                    {
                        processing = messageToSend;

                        Payload payload = messageToSend.Payload;
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

                            byte[] bytes = ms.ToArray();

                            this.Client.SendAsync(bytes, this.Cancel.Token).GetAwaiter().GetResult();
                            this.Peer.Counter.AddWritten(bytes.Length);
                            processing.Completion.SetResult(true);
                            processing = null;
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
                    pending.Completion.SetException(new OperationCanceledException("The peer has been disconnected"));
                }

                this.Messages = new BlockingCollection<SentMessage>(new ConcurrentQueue<SentMessage>());

                this.logger.LogDebug("Terminating sending thread.");
                this.EndListen(unhandledException);

                this.logger.LogTrace("(-)");
            }).Start();

            // This is receiving thread.
            new Thread(() =>
            {
                this.logger.LogTrace("()");

                this.logger.LogTrace("Start listenting.");
                Exception unhandledException = null;
                byte[] buffer = new byte[1024 * 1024];
                try
                {
                    while (!this.Cancel.Token.IsCancellationRequested)
                    {
                        PerformanceCounter counter;

                        Message message = Message.ReadNext(this.Client.Stream, this.Peer.Network, this.Peer.Version, this.Cancel.Token, buffer, out counter);

                        this.logger.LogTrace("Receiving message: '{0}'", message);

                        this.Peer.LastSeen = this.dateTimeProvider.GetUtcNow();
                        this.Peer.Counter.Add(counter);
                        this.Peer.OnMessageReceived(new IncomingMessage()
                        {
                            Message = message,
                            Client = this.Client,
                            Length = counter.ReadBytes,
                            NetworkPeer = this.Peer
                        });
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

            this.Client.Dispose();
            this.Client.ProcessingCompletion.SetResult(true);

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

        /// <summary>State of the network connection to the peer.</summary>
        private volatile NetworkPeerState state = NetworkPeerState.Offline;
        /// <summary>State of the network connection to the peer.</summary>
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

        /// <summary>IP address and port of the connected peer.</summary>
        public IPEndPoint RemoteSocketEndpoint { get; private set; }

        /// <summary>IP address part of <see cref="RemoteSocketEndpoint"/>.</summary>
        public IPAddress RemoteSocketAddress { get; private set; }

        /// <summary>Port part of <see cref="RemoteSocketEndpoint"/>.</summary>
        public int RemoteSocketPort { get; private set; }

        /// <summary><c>true</c> if the peer connected to the node, <c>false</c> if the node connected to the peer.</summary>
        public bool Inbound { get; private set; }

        /// <summary>List of node's modules attached to the peer to receive notifications about various events related to the peer.</summary>
        public NetworkPeerBehaviorsCollection Behaviors { get; private set; }

        /// <summary>Information about the peer including its network address, protocol version, time of last contact.</summary>
        public NetworkAddress PeerAddress { get; private set; }

        /// <summary>Last time in UTC the node received something from this peer.</summary>
        public DateTime LastSeen { get; set; }

        /// <summary>Difference between the local clock and the clock that peer claims, or <c>null</c> if this information has not been initialized yet.</summary>
        public TimeSpan? TimeOffset { get; private set; }

        /// <summary>Component representing the network connection to the peer that is responsible for sending and receiving messages.</summary>
        internal readonly NetworkPeerConnection Connection;

        /// <summary>Statistics about the number of bytes transferred from and to the peer.</summary>
        private PerformanceCounter counter;
        /// <summary>Statistics about the number of bytes transferred from and to the peer.</summary>
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

        /// <summary><c>true</c> if the connection to the peer is considered active, <c>false</c> otherwise, including any case of error.</summary>
        public bool IsConnected
        {
            get
            {
                return (this.State == NetworkPeerState.Connected) || (this.State == NetworkPeerState.HandShaked);
            }
        }

        /// <summary>Queue of incoming messages distributed to message consumers.</summary>
        public MessageProducer<IncomingMessage> MessageProducer { get; private set; }

        /// <summary><c>true</c> to advertise "addr" message with our external endpoint to the peer when passing to <see cref="NetworkPeerState.HandShaked"/> state.</summary>
        public bool Advertize { get; set; }

        /// <summary>Node's version message payload that is sent to the peer.</summary>
        public VersionPayload MyVersion { get; private set; }

        /// <summary>Version message payload received from the peer.</summary>
        public VersionPayload PeerVersion { get; private set; }

        /// <summary>Set to <c>1</c> if the peer disconnection has been initiated, <c>0</c> otherwise.</summary>
        private int disconnecting;

        /// <summary>Transaction options we would like.</summary>
        private NetworkOptions preferredTransactionOptions;

        /// <summary>Transaction options supported by the peer.</summary>
        public NetworkOptions SupportedTransactionOptions { get; private set; }

        /// <summary>Transaction options we prefer and which is also supported by peer.</summary>
        private NetworkOptions actualTransactionOptions
        {
            get
            {
                return this.preferredTransactionOptions & this.SupportedTransactionOptions;
            }
        }

        /// <summary>When a peer is disconnected this is set to human readable information about why it happened.</summary>
        public NetworkPeerDisconnectReason DisconnectReason { get; set; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; set; }

        /// <summary>Event that is triggered on network peer disconnection.</summary>
        public event NetworkPeerStateChangedEventHandler StateChanged;

        /// <summary>Event that is triggered when a new message is received from a network peer.</summary>
        public event NetworkPeerMessageReceivedEventHandler MessageReceived;

        /// <summary>
        /// Event that is triggered when a new message is received from a network peer.
        /// <para>This event is triggered before <see cref="MessageReceived"/>.</para>
        /// </summary>
        /// <seealso cref="Stratis.Bitcoin.Base.ChainHeadersBehavior.AttachCore"/>
        /// <remarks>TODO: Remove this once the events are refactored.</remarks>
        public event NetworkPeerMessageReceivedEventHandler MessageReceivedPriority;

        /// <summary>Event handler that is triggered when the network state of a peer was changed.</summary>
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

        /// <summary>
        /// Initializes parts of the object that are common for both inbound and outbound peers.
        /// </summary>
        /// <param name="inbound"><c>true</c> for inbound peers, <c>false</c> for outbound peers.</param>
        /// <param name="peerAddress">Information about the peer including its network address, protocol version, time of last contact.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, or <c>null</c> to use default parameters.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        private NetworkPeer(bool inbound, NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{peerAddress.Endpoint}] ");
            this.dateTimeProvider = dateTimeProvider;

            this.MessageProducer = new MessageProducer<IncomingMessage>();

            this.preferredTransactionOptions = NetworkOptions.All;
            this.SupportedTransactionOptions = NetworkOptions.None;

            this.Inbound = inbound;
            this.LastSeen = peerAddress.Time.UtcDateTime;
            this.PeerAddress = peerAddress;
            this.Network = network;
            this.Behaviors = new NetworkPeerBehaviorsCollection(this);

            parameters = parameters ?? new NetworkPeerConnectionParameters();
            this.MyVersion = parameters.CreateVersion(peerAddress.Endpoint, network, this.dateTimeProvider.GetTimeOffset());
        }

        /// <summary>
        /// Initializes an instance of the object for outbound network peers.
        /// </summary>
        /// <param name="peerAddress">Information about the peer including its network address, protocol version, time of last contact.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, or <c>null</c> to use default parameters.</param>
        /// <param name="networkPeerFactory">Factory for creating P2P network peers.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public NetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, INetworkPeerFactory networkPeerFactory, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
            : this(false, peerAddress, network, parameters, dateTimeProvider, loggerFactory)
        {
            this.logger.LogTrace("()");

            NetworkPeerClient client = networkPeerFactory.CreateNetworkPeerClient(parameters);
            this.Connection = new NetworkPeerConnection(this, client, this.dateTimeProvider, this.loggerFactory);
            this.ConnectAsync(parameters.ConnectCancellation).GetAwaiter().GetResult();

            this.InitDefaultBehaviors(parameters);
            this.Connection.BeginListen();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Initializes an instance of the object for inbound network peers with already established connection.
        /// </summary>
        /// <param name="peerAddress">Information about the peer including its network address, protocol version, time of last contact.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, or <c>null</c> to use default parameters.</param>
        /// <param name="client">Already connected network client.</param>
        /// <param name="peerVersion">Version message payload received from the peer.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public NetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, NetworkPeerClient client, VersionPayload peerVersion, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
            : this(true, peerAddress, network, parameters, dateTimeProvider, loggerFactory)
        {
            this.logger.LogTrace("()");

            this.RemoteSocketEndpoint = client.RemoteEndPoint;
            this.RemoteSocketAddress = this.RemoteSocketEndpoint.Address;
            this.RemoteSocketPort = this.RemoteSocketEndpoint.Port;

            this.PeerVersion = peerVersion;
            this.Connection = new NetworkPeerConnection(this, client, this.dateTimeProvider, this.loggerFactory);
            this.ConnectedAt = this.dateTimeProvider.GetUtcNow();

            this.logger.LogTrace("Connected to advertised node '{0}'.", this.PeerAddress.Endpoint);
            this.State = NetworkPeerState.Connected;

            this.InitDefaultBehaviors(parameters);
            this.Connection.BeginListen();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Connects the node to an outbound peer using already initialized information about the peer.
        /// </summary>
        /// <param name="cancellation">Cancellation that allows aborting establishing the connection with the peer.</param>
        private async Task ConnectAsync(CancellationToken cancellation)
        {
            this.logger.LogTrace("()");

            try
            {
                this.logger.LogTrace("Connecting to '{0}'.", this.PeerAddress.Endpoint);

                await this.Connection.Client.ConnectAsync(this.PeerAddress.Endpoint, cancellation);

                this.RemoteSocketEndpoint = this.Connection.Client.RemoteEndPoint;
                this.RemoteSocketAddress = this.RemoteSocketEndpoint.Address;
                this.RemoteSocketPort = this.RemoteSocketEndpoint.Port;

                this.State = NetworkPeerState.Connected;
                this.ConnectedAt = this.dateTimeProvider.GetUtcNow();

                this.logger.LogTrace("Outbound connection to '{0}' established.", this.PeerAddress.Endpoint);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("Connection to '{0}' cancelled.", this.PeerAddress.Endpoint);
                this.Connection.Client.Dispose();
                this.State = NetworkPeerState.Offline;

                this.logger.LogTrace("(-)[CANCELLED]");
                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                this.Connection.Client.Dispose();
                this.DisconnectReason = new NetworkPeerDisconnectReason()
                {
                    Reason = "Unexpected exception while connecting to socket",
                    Exception = ex
                };

                this.State = NetworkPeerState.Failed;

                this.logger.LogTrace("(-)[EXCEPTION]");
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Calls event handlers when the network state of the peer is changed.
        /// </summary>
        /// <param name="previous">Previous network state of the peer.</param>
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

        /// <summary>
        /// Calls event handlers when a new message is received from the peer.
        /// </summary>
        /// <param name="message">Message that was received.</param>
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

            this.MessageProducer.PushMessage(message);
            NetworkPeerMessageReceivedEventHandler messageReceivedPriority = MessageReceivedPriority;
            if (messageReceivedPriority != null)
            {
                foreach (NetworkPeerMessageReceivedEventHandler handler in messageReceivedPriority.GetInvocationList().Cast<NetworkPeerMessageReceivedEventHandler>())
                {
                    try
                    {
                        handler.DynamicInvoke(this, message);
                    }
                    catch (TargetInvocationException ex)
                    {
                        this.logger.LogError("Exception occurred: {0}", ex.InnerException.ToString());
                    }
                }
            }

            NetworkPeerMessageReceivedEventHandler messageReceived = MessageReceived;
            if (messageReceived != null)
            {
                foreach (NetworkPeerMessageReceivedEventHandler handler in messageReceived.GetInvocationList().Cast<NetworkPeerMessageReceivedEventHandler>())
                {
                    try
                    {
                        handler.DynamicInvoke(this, message);
                    }
                    catch (TargetInvocationException ex)
                    {
                        this.logger.LogError("Exception occurred: {0}", ex.InnerException.ToString());
                    }
                }
            }


            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Calls event handlers when the peer is disconnected from the node.
        /// </summary>
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

        /// <summary>
        /// Initializes behaviors from the default template.
        /// </summary>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, including the default behaviors template.</param>
        private void InitDefaultBehaviors(NetworkPeerConnectionParameters parameters)
        {
            this.logger.LogTrace("()");

            this.Advertize = parameters.Advertize;
            this.preferredTransactionOptions = parameters.PreferredTransactionOptions;

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
        /// <exception cref="OperationCanceledException">Thrown when the peer has been disconnected.</param>
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

            this.Connection.Messages.Add(new SentMessage()
            {
                Payload = payload,
                Completion = completion
            });

            this.logger.LogTrace("(-)");
            return completion.Task;
        }

        /// <summary>
        /// Send a message to the peer synchronously.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <exception cref="OperationCanceledException">Thrown when the peer has been disconnected or the cancellation token has been cancelled.</param>
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

        /// <summary>
        /// Waits for a message of specific type to be received from the peer.
        /// </summary>
        /// <typeparam name="TPayload">Type of message to wait for.</typeparam>
        /// <param name="timeout">How long to wait for the message.</param>
        /// <returns>Received message.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the wait timed out.</exception>
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

        /// <summary>
        /// Waits for a message of specific type to be received from the peer.
        /// </summary>
        /// <typeparam name="TPayload">Type of message to wait for.</typeparam>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation.</param>
        /// <returns>Received message.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the cancellation token was cancelled.</exception>
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

        /// <summary>
        /// Exchanges "version" and "verack" messages with the peer.
        /// <para>Both parties have to send their "version" messages to the other party 
        /// as well as to acknowledge that they are happy with the other party's "version" information.</para>
        /// </summary>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        public void VersionHandshake(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.VersionHandshake(null, cancellationToken);
        }

        /// <summary>
        /// Exchanges "version" and "verack" messages with the peer.
        /// <para>Both parties have to send their "version" messages to the other party 
        /// as well as to acknowledge that they are happy with the other party's "version" information.</para>
        /// </summary>
        /// <param name="requirements">Protocol requirement for network peers the node wants to be connected to.</param>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
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
        /// Sends "version" message to the peer and waits for the response in form of "verack" or "reject" message.
        /// </summary>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        public void RespondToHandShake(CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            using (NetworkPeerListener list = this.CreateListener().Where(m => (m.Message.Payload is VerAckPayload) || (m.Message.Payload is RejectPayload)))
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
        /// Disconnects the peer and cleans up.
        /// </summary>
        /// <param name="reason">Human readable reason for disconnecting.</param>
        public void Disconnect(string reason = null)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(reason), reason);

            if (this.IsConnected == false)
            {
                this.logger.LogTrace("(-)[NOT_CONNECTED]");
                return;
            }

            this.DisconnectInternal(reason, null);

            try
            {
                this.Connection.Disconnected.WaitOne();
            }
            finally
            {
                this.Connection.CleanUp();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Disconnects the peer and cleans up.
        /// </summary>
        /// <param name="reason">Human readable reason for disconnecting.</param>
        /// <param name="exception">Exception because of which the disconnection happened, or <c>null</c> if there were no exception.</param>
        public void DisconnectWithException(string reason = null, Exception exception = null)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(reason), reason);

            if (this.IsConnected == false)
            {
                this.logger.LogTrace("(-)[NOT_CONNECTED]");
                return;
            }

            this.DisconnectInternal(reason, exception);
            this.Connection.CleanUp();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Disconnects the peer and cleans up.
        /// </summary>
        /// <param name="reason">Human readable reason for disconnecting.</param>
        /// <param name="exception">Exception because of which the disconnection happened, or <c>null</c> if there were no exception.</param>
        private void DisconnectInternal(string reason = null, Exception exception = null)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(reason), reason);

            if (Interlocked.CompareExchange(ref this.disconnecting, 1, 0) == 1)
            {
                this.logger.LogTrace("(-)[DISCONNECTING");
                return;
            }

            this.State = NetworkPeerState.Disconnecting;
            this.Connection.Cancel.Cancel();

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

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0} ({1})", this.State, this.PeerAddress.Endpoint);
        }

        /// <summary>
        /// Create a listener that will queue messages received from the peer until it is disposed.
        /// </summary>
        /// <returns>The listener.</returns>
        public NetworkPeerListener CreateListener()
        {
            return new NetworkPeerListener(this);
        }

        /// <summary>
        /// Add supported option to the inventory type.
        /// </summary>
        /// <param name="inventoryType">Inventory type to extend.</param>
        /// <returns>Inventory type possibly extended with new options.</returns>
        public InventoryType AddSupportedOptions(InventoryType inventoryType)
        {
            if ((this.actualTransactionOptions & NetworkOptions.Witness) != 0)
                inventoryType |= InventoryType.MSG_WITNESS_FLAG;

            return inventoryType;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Disconnect("Node disposed");
        }
    }
}