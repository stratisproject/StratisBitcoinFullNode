using System;
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

    /// <summary>
    /// Represents a counterparty of the node on the network. This is usually another node, but it can be 
    /// a wallet, an analytical robot, or any other network client or server that understands the protocol.
    /// <para>The network peer is either inbound, if it was the counterparty that established the connection to our 
    /// node's listener, or outbound, if our node was the one connecting to a remote server.
    /// </para>
    /// </summary>
    /// <remarks>
    /// All instances of this object must be disposed or disconnected. <see cref="Disconnect(string, Exception)"/> and disposing methods 
    /// have the same functionality and the disconnecting method is provided only for better readability of the code. 
    /// <para>It is safe to try to disconnect or dispose this object multiple times, only the first call will be processed.</para>
    /// </remarks>
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

        /// <summary><c>true</c> to advertise "addr" message with our external endpoint to the peer when passing to <see cref="NetworkPeerState.HandShaked"/> state.</summary>
        public bool Advertize { get; set; }

        /// <summary>Node's version message payload that is sent to the peer.</summary>
        public VersionPayload MyVersion { get; private set; }

        /// <summary>Version message payload received from the peer.</summary>
        public VersionPayload PeerVersion { get; private set; }

        /// <summary>Set to <c>1</c> if the peer disconnection has been initiated, <c>0</c> otherwise.</summary> 
        private int disconnected;

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

        /// <summary>Queue of the connections' incoming messages distributed to message consumers.</summary>
        public MessageProducer<IncomingMessage> MessageProducer { get { return this.Connection.MessageProducer; } }

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
            this.PeerAddress = new NetworkAddress();
            NetworkPeerClient client = new NetworkPeerClient(0, new TcpClient(), this.Network, this.loggerFactory);
            this.Connection = new NetworkPeerConnection(this, client, this.ProcessMessageAsync, this.dateTimeProvider, this.loggerFactory);
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
            this.dateTimeProvider = dateTimeProvider;

            this.preferredTransactionOptions = network.NetworkOptions;
            this.SupportedTransactionOptions = network.NetworkOptions & ~NetworkOptions.All;

            this.Inbound = inbound;
            this.LastSeen = peerAddress.Time.UtcDateTime;
            this.PeerAddress = peerAddress;
            this.Network = network;
            this.Behaviors = new NetworkPeerBehaviorsCollection(this);

            this.Parameters = parameters ?? new NetworkPeerConnectionParameters();
            this.MyVersion = this.Parameters.CreateVersion(this.PeerAddress.Endpoint, network, this.dateTimeProvider.GetTimeOffset());
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
            NetworkPeerClient networkPeerClient = networkPeerFactory.CreateNetworkPeerClient(parameters);
            this.Connection = new NetworkPeerConnection(this, networkPeerClient, this.ProcessMessageAsync, this.dateTimeProvider, this.loggerFactory);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{networkPeerClient.Id}-{peerAddress.Endpoint}] ");
            this.logger.LogTrace("()");

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
        public NetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, TcpClient client, IDateTimeProvider dateTimeProvider, INetworkPeerFactory networkPeerFactory, ILoggerFactory loggerFactory)
            : this(true, peerAddress, network, parameters, dateTimeProvider, loggerFactory)
        {
            NetworkPeerClient networkPeerClient = networkPeerFactory.CreateNetworkPeerClient(client);
            this.Connection = new NetworkPeerConnection(this, networkPeerClient, this.ProcessMessageAsync, this.dateTimeProvider, this.loggerFactory);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{networkPeerClient.Id}-{peerAddress.Endpoint}] ");
            this.logger.LogTrace("()");

            this.RemoteSocketEndpoint = this.PeerAddress.Endpoint;
            this.RemoteSocketAddress = this.RemoteSocketEndpoint.Address;
            this.RemoteSocketPort = this.RemoteSocketEndpoint.Port;

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
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(previous), previous, nameof(this.State), this.State);

            NetworkPeerStateChangedEventHandler stateChanged = this.StateChanged;
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
        /// Processes an incoming message from the peer and calls subscribed event handlers.
        /// </summary>
        /// <param name="message">Message received from the peer.</param>
        public async Task ProcessMessageAsync(IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(message), message.Message.Command);

            switch (message.Message.Payload)
            {
                case VersionPayload versionPayload:
                    await this.ProcessVersionMessageAsync(versionPayload).ConfigureAwait(false);
                    break;

                case HaveWitnessPayload unused:
                    this.SupportedTransactionOptions |= NetworkOptions.Witness;
                    break;
            }

            this.CallMessageReceivedHandlers(message);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Calls event handlers when a new message is received from the peer.
        /// </summary>
        /// <param name="message">Message that was received.</param>
        private void CallMessageReceivedHandlers(IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(message), message.Message.Command);

            NetworkPeerMessageReceivedEventHandler messageReceivedPriority = this.MessageReceivedPriority;
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

            NetworkPeerMessageReceivedEventHandler messageReceived = this.MessageReceived;
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
        /// Processes a "version" message received from a peer.
        /// </summary>
        /// <param name="version">Version message received from a peer.</param>
        private async Task ProcessVersionMessageAsync(VersionPayload version)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(version), version);

            this.logger.LogTrace("Peer's state is {0}.", this.State);

            switch (this.State)
            {
                case NetworkPeerState.Connected:
                    if (this.Inbound) await this.ProcessInitialVersionPayloadAsync(version).ConfigureAwait(false);
                    break;

                case NetworkPeerState.HandShaked:
                    if (this.Version >= ProtocolVersion.REJECT_VERSION)
                    {
                        var rejectPayload = new RejectPayload()
                        {
                            Code = RejectCode.DUPLICATE
                        };

                        await this.SendMessageAsync(rejectPayload).ConfigureAwait(false);
                    }
                    break;
            }

            this.TimeOffset = this.dateTimeProvider.GetTimeOffset() - version.Timestamp;
            if ((version.Services & NetworkPeerServices.NODE_WITNESS) != 0)
                this.SupportedTransactionOptions |= NetworkOptions.Witness;


            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes an initial "version" message received from a peer.
        /// </summary>
        /// <param name="version">Version message received from a peer.</param>
        /// <exception cref="OperationCanceledException">Thrown if the response to our "version" message is not received on time.</exception>
        private async Task ProcessInitialVersionPayloadAsync(VersionPayload version)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(version), version);

            this.PeerVersion = version;
            bool connectedToSelf = version.Nonce == this.Parameters.Nonce;

            this.logger.LogDebug("First message received from peer '{0}'.", version.AddressFrom);

            if (connectedToSelf)
            {
                this.logger.LogDebug("Connection to self detected and will be aborted.");

                VersionPayload versionPayload = this.Parameters.CreateVersion(this.PeerAddress.Endpoint, this.Network, this.dateTimeProvider.GetTimeOffset());
                await this.SendMessageAsync(versionPayload);
                this.Disconnect("Connected to self");

                this.logger.LogTrace("(-)[CONNECTED_TO_SELF]");
                return;
            }

            using (CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(this.Connection.CancellationSource.Token))
            {
                cancellationSource.CancelAfter(TimeSpan.FromSeconds(10.0));
                try
                {
                    await this.RespondToHandShakeAsync(cancellationSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("Remote peer haven't responded within 10 seconds of the handshake completion, dropping connection.");

                    this.Disconnect("Handshake timeout");

                    this.logger.LogTrace("(-)[HANDSHAKE_TIMEDOUT]");
                    throw;
                }
                catch (Exception ex)
                {
                    this.logger.LogTrace("Exception occurred: {0}", ex.ToString());

                    this.Disconnect("Handshake exception", ex);

                    this.logger.LogTrace("(-)[HANDSHAKE_EXCEPTION]");
                    throw;
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
                TPayload res = this.ReceiveMessage<TPayload>(source.Token);

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
        public async Task VersionHandshakeAsync(NetworkPeerRequirement requirements, CancellationToken cancellationToken)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(requirements), nameof(requirements.RequiredServices), requirements?.RequiredServices);

            requirements = requirements ?? new NetworkPeerRequirement();
            using (var listener = new NetworkPeerListener(this).Where(p => (p.Message.Payload is VersionPayload)
                || (p.Message.Payload is RejectPayload)
                || (p.Message.Payload is VerAckPayload)))
            {
                this.logger.LogTrace("Sending my version.");
                await this.SendMessageAsync(this.MyVersion).ConfigureAwait(false);

                this.logger.LogTrace("Waiting for version or rejection message.");
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

                this.logger.LogTrace("Sending version acknowledgement.");
                await this.SendMessageAsync(new VerAckPayload()).ConfigureAwait(false);

                this.logger.LogTrace("Waiting for version acknowledgement.");
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
                this.logger.LogTrace("Responding to handshake with my version.");
                await this.SendMessageAsync(this.MyVersion);

                this.logger.LogTrace("Waiting for version acknowledgement or rejection message.");
                IncomingMessage message = listener.ReceiveMessage(cancellation);

                if (message.Message.Payload is RejectPayload reject)
                {
                    this.logger.LogTrace("Version rejected: code {0}, reason '{1}'.", reject.Code, reject.Reason);
                    this.logger.LogTrace("(-)[VERSION_REJECTED]");
                    throw new ProtocolException("Version rejected " + reject.Code + ": " + reject.Reason);
                }

                this.logger.LogTrace("Sending version acknowledgement.");
                await this.SendMessageAsync(new VerAckPayload());
                this.State = NetworkPeerState.HandShaked;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Disconnects the peer and cleans up.
        /// </summary>
        /// <param name="reason">Human readable reason for disconnecting.</param>
        /// <param name="exception">Exception because of which the disconnection happened, or <c>null</c> if there were no exception.</param>
        public void Disconnect(string reason, Exception exception = null)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(reason), reason);

            if (Interlocked.CompareExchange(ref this.disconnected, 1, 0) == 1)
            {
                this.logger.LogTrace("(-)[DISCONNECTED]");
                return;
            }

            if (!this.Connection.CancellationSource.IsCancellationRequested)
            {
                if (this.IsConnected) this.State = NetworkPeerState.Disconnecting;

                this.Connection.CancellationSource.Cancel();
            }

            this.Connection.Disconnected.WaitHandle.WaitOne();
            this.Connection.Dispose();

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

        /// <summary>
        /// Disconnects the peer and cleans up.
        /// </summary>
        /// <param name="reason">Human readable reason for disconnecting.</param>
        /// <param name="exception">Exception because of which the disconnection happened, or <c>null</c> if there were no exception.</param>
        public void Dispose(string reason, Exception exception = null)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(reason), reason);

            this.Disconnect(reason, exception);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose("Peer disposed");
        }
    }
}