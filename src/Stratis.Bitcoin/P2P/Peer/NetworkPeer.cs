using System;
using System.IO;
using System.Linq;
using System.Net;
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

    /// <summary>
    /// Represents a network connection to a peer. It is responsible for reading incoming messages from the peer 
    /// and sending messages from the node to the peer.
    /// </summary>
    public class NetworkPeerConnection : IDisposable
    {
        /// <summary>Logger for the node.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Network peer this connection connects to.</summary>
        public NetworkPeer Peer { get; private set; }

        /// <summary>Connected network socket to the peer.</summary>
        public NetworkPeerClient Client { get; private set; }

        /// <summary>Event that is set when the connection is closed.</summary>
        public ManualResetEvent Disconnected { get; private set; }

        /// <summary>Cancellation to be triggered at shutdown to abort all pending operations on the connection.</summary>
        public CancellationTokenSource CancellationSource { get; private set; }

        /// <summary>Registration of callback routine to shutdown the connection when <see cref="CancellationSource"/>'s token is cancelled.</summary>
        private CancellationTokenRegistration cancelRegistration;

        /// <summary>Task responsible for reading incoming messages from the stream.</summary>
        private Task receiveMessageTask;

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
            this.CancellationSource = new CancellationTokenSource();
            this.cancelRegistration = this.CancellationSource.Token.Register(this.InitiateShutdown);
            this.Disconnected = new ManualResetEvent(false);
        }

        /// <summary>
        /// Sends message to the connected counterparty.
        /// </summary>
        /// <param name="payload">Payload of the message to send.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the sending operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the peer has been disconnected or the cancellation token has been cancelled.</param>
        public async Task SendAsync(Payload payload, CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(payload), payload);

            CancellationTokenSource cts = null;
            if (cancellation != default(CancellationToken))
            {
                cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, this.CancellationSource.Token);
                cancellation = cts.Token;
            }
            else cancellation = this.CancellationSource.Token;

            try
            {

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

                    await this.Client.SendAsync(bytes, cancellation).ConfigureAwait(false);
                    this.Peer.Counter.AddWritten(bytes.Length);
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    this.logger.LogTrace("Sending cancelled.");
                }
                else
                {
                    this.logger.LogTrace("Exception occurred: '{0}'", ex.ToString());

                    this.Peer.State = NetworkPeerState.Failed;
                    this.Peer.DisconnectReason = new NetworkPeerDisconnectReason()
                    {
                        Reason = "Unexpected exception while sending a message",
                        Exception = ex
                    };
                }

                this.CancellationSource.Cancel();
            }
            finally
            {
                cts?.Dispose();
            }
            finally
            {
                cts?.Dispose();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Starts waiting for incoming messages.
        /// </summary>
        public void StartReceiveMessages()
        {
            this.logger.LogTrace("()");

            this.receiveMessageTask = ReceiveMessagesAsync();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Reads messages from the connection stream.
        /// </summary>
        public async Task ReceiveMessagesAsync()
        { 
            this.logger.LogTrace("()");

            try
            {
                while (!this.CancellationSource.Token.IsCancellationRequested)
                {
                    Message message = await this.Client.ReadAndParseMessageAsync(this.Peer.Version, this.CancellationSource.Token).ConfigureAwait(false);

                    this.logger.LogTrace("Received message: '{0}'", message);

                    this.Peer.LastSeen = this.dateTimeProvider.GetUtcNow();
                    this.Peer.Counter.AddRead(message.MessageSize);
                    this.Peer.OnMessageReceived(new IncomingMessage()
                    {
                        Message = message,
                        Client = this.Client,
                        Length = message.MessageSize,
                        NetworkPeer = this.Peer
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    this.logger.LogTrace("Receiving cancelled.");
                }
                else
                {
                    this.logger.LogTrace("Exception occurred: '{0}'", ex.ToString());

                    this.Peer.State = NetworkPeerState.Failed;
                    this.Peer.DisconnectReason = new NetworkPeerDisconnectReason()
                    {
                        Reason = "Unexpected exception while waiting for a message",
                        Exception = ex
                    };
                }

                this.CancellationSource.Cancel();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// When the connection is terminated, this method cleans up and informs connected behaviors about the termination.
        /// </summary>
        private void InitiateShutdown()
        {
            this.logger.LogTrace("()");

            if (this.Peer.State != NetworkPeerState.Failed)
                this.Peer.State = NetworkPeerState.Offline;

            this.Client.Dispose();
            this.Client.ProcessingCompletion.SetResult(true);
            this.Disconnected.Set();

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

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            if (this.CancellationSource.IsCancellationRequested == false)
                this.CancellationSource.Cancel();

            this.receiveMessageTask.Wait();
            this.Disconnected.WaitOne();

            this.Disconnected.Dispose();
            this.CancellationSource.Dispose();
            this.cancelRegistration.Dispose();

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
        /// The negotiated protocol version (minimum of supported version between <see cref="MyVersion"/> and the <see cref="PeerVersion"/>).
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

        /// <summary>Various settings and requirements related to how the connections with peers are going to be established.</summary>
        public NetworkPeerConnectionParameters Parameters { get; private set; }

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

            this.preferredTransactionOptions = network.NetworkOptions;
            this.SupportedTransactionOptions = network.NetworkOptions & ~NetworkOptions.All;

            this.Inbound = inbound;
            this.LastSeen = peerAddress.Time.UtcDateTime;
            this.PeerAddress = peerAddress;
            this.Network = network;
            this.Behaviors = new NetworkPeerBehaviorsCollection(this);

            this.Parameters = parameters ?? new NetworkPeerConnectionParameters();
            this.MyVersion = this.Parameters.CreateVersion(peerAddress.Endpoint, network, this.dateTimeProvider.GetTimeOffset());
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

            this.InitDefaultBehaviors(this.Parameters);
            this.Connection.StartReceiveMessages();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Connects the node to an outbound peer using already initialized information about the peer and starts receiving messages in a separate task.
        /// </summary>
        /// <param name="cancellation">Cancellation that allows aborting establishing the connection with the peer.</param>
        /// <exception cref="OperationCanceledException">Thrown when the cancellation token has been cancelled.</exception>
        public async Task ConnectAsync(CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            try
            {
                this.logger.LogTrace("Connecting to '{0}'.", this.PeerAddress.Endpoint);

                await this.Connection.Client.ConnectAsync(this.PeerAddress.Endpoint, cancellation).ConfigureAwait(false);

                this.RemoteSocketEndpoint = this.Connection.Client.RemoteEndPoint;
                this.RemoteSocketAddress = this.RemoteSocketEndpoint.Address;
                this.RemoteSocketPort = this.RemoteSocketEndpoint.Port;

                this.State = NetworkPeerState.Connected;
                this.ConnectedAt = this.dateTimeProvider.GetUtcNow();

                this.InitDefaultBehaviors(this.Parameters);
                this.Connection.StartReceiveMessages();

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
                {
                    message.NetworkPeer.SendMessageVoidAsync(new RejectPayload()
                    {
                        Code = RejectCode.DUPLICATE
                    });
                }
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
        /// Send a message to the peer asynchronously and ignores the returned task.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <exception cref="OperationCanceledException">Thrown when the peer has been disconnected.</param>
        /// <remarks>
        /// TODO: Remove this method from the code base as it is a bad practise to use it anyway.
        /// If we used proper SendMessageAsync instead, it would throw an exception if the connection to the peer 
        /// is terminated, which is what we want - detect the failure as early as possible and not to advance 
        /// in the code in such a case. Also most of the time we send the message and wait for the response, 
        /// in which case we save nothing by sending the message and not awaiting the send operation.
        /// </remarks>
        public void SendMessageVoidAsync(Payload payload)
        {
            Task unused = this.SendMessageAsync(payload);
        }

        /// <summary>
        /// Send a message to the peer asynchronously.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the sending operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the peer has been disconnected or the cancellation token has been cancelled.</param>
        public async Task SendMessageAsync(Payload payload, CancellationToken cancellation = default(CancellationToken))
        {
            Guard.NotNull(payload, nameof(payload));
            this.logger.LogTrace("({0}:'{1}')", nameof(payload), payload);

            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            if (!this.IsConnected)
            {
                this.logger.LogTrace("(-)[NOT_CONNECTED]");
                throw new OperationCanceledException("The peer has been disconnected");
            }

            await this.Connection.SendAsync(payload, cancellation).ConfigureAwait(false);

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
        public async Task VersionHandshakeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.VersionHandshakeAsync(null, cancellationToken);
        }

        /// <summary>
        /// Exchanges "version" and "verack" messages with the peer.
        /// <para>Both parties have to send their "version" messages to the other party 
        /// as well as to acknowledge that they are happy with the other party's "version" information.</para>
        /// </summary>
        /// <param name="requirements">Protocol requirement for network peers the node wants to be connected to.</param>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        public async Task VersionHandshakeAsync(NetworkPeerRequirement requirements, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(requirements), nameof(requirements.RequiredServices), requirements?.RequiredServices);

            requirements = requirements ?? new NetworkPeerRequirement();
            using (var listener = new NetworkPeerListener(this).Where(p => (p.Message.Payload is VersionPayload)
                || (p.Message.Payload is RejectPayload)
                || (p.Message.Payload is VerAckPayload)))
            {
                await this.SendMessageAsync(this.MyVersion).ConfigureAwait(false);
                Payload payload = listener.ReceivePayload<Payload>(cancellationToken);
                if (payload is RejectPayload)
                {
                    this.logger.LogTrace("(-)[HANDSHAKE_REJECTED]");
                    throw new ProtocolException("Handshake rejected: " + ((RejectPayload)payload).Reason);
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

                await this.SendMessageAsync(new VerAckPayload()).ConfigureAwait(false);
                listener.ReceivePayload<VerAckPayload>(cancellationToken);
                this.State = NetworkPeerState.HandShaked;

                if (this.Advertize && this.MyVersion.AddressFrom.Address.IsRoutable(true))
                {
                    this.SendMessageVoidAsync(new AddrPayload(new NetworkAddress(this.MyVersion.AddressFrom)
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
        /// <exception cref="ProtocolException">Thrown when the peer rejected our "version" message.</exception>
        public async Task RespondToHandShakeAsync(CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            using (var listener = new NetworkPeerListener(this).Where(m => (m.Message.Payload is VerAckPayload) || (m.Message.Payload is RejectPayload)))
            {
                this.logger.LogTrace("Responding to handshake.");
                await this.SendMessageAsync(this.MyVersion);
                IncomingMessage message = listener.ReceiveMessage(cancellation);

                if (message.Message.Payload is RejectPayload reject)
                {
                    this.logger.LogTrace("Version rejected: code {0}, reason {1}.", reject.Code, reject.Reason);
                    this.logger.LogTrace("(-)[VERSION_REJECTED]");
                    throw new ProtocolException("Version rejected " + reject.Code + " : " + reject.Reason);
                }

                await this.SendMessageAsync(new VerAckPayload());
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
                this.Connection.Dispose();
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
            this.Connection.Dispose();

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
            this.Connection.CancellationSource.Cancel();

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