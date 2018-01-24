using System;
using System.Net;
using System.Net.Sockets;
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
        public NetworkPeerState State { get; private set; }

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

        /// <summary>IP address and port on the side of the peer.</summary>
        public IPEndPoint PeerEndPoint { get; private set; }

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

        /// <summary>Event that is triggered when the peer's network state is changed.</summary>
        public readonly AsyncExecutionEvent<NetworkPeer, NetworkPeerState> StateChanged;

        /// <summary>Event that is triggered when a new message is received from a network peer.</summary>
        public readonly AsyncExecutionEvent<NetworkPeer, IncomingMessage> MessageReceived;

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
            this.PeerEndPoint = new IPEndPoint(IPAddress.Loopback, 1);
            this.Connection = new NetworkPeerConnection(null, this, new TcpClient(), 0, this.ProcessMessageAsync, this.dateTimeProvider, this.loggerFactory);
            this.MessageReceived = new AsyncExecutionEvent<NetworkPeer, IncomingMessage>();
            this.StateChanged = new AsyncExecutionEvent<NetworkPeer, NetworkPeerState>();
        }

        /// <summary>
        /// Initializes parts of the object that are common for both inbound and outbound peers.
        /// </summary>
        /// <param name="inbound"><c>true</c> for inbound peers, <c>false</c> for outbound peers.</param>
        /// <param name="peerEndPoint">IP address and port on the side of the peer.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, or <c>null</c> to use default parameters.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        private NetworkPeer(bool inbound, IPEndPoint peerEndPoint, Network network, NetworkPeerConnectionParameters parameters, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.dateTimeProvider = dateTimeProvider;

            this.preferredTransactionOptions = network.NetworkOptions;
            this.SupportedTransactionOptions = network.NetworkOptions & ~NetworkOptions.All;

            this.State = NetworkPeerState.Offline;
            this.Inbound = inbound;
            this.LastSeen = this.dateTimeProvider.GetUtcNow();
            this.PeerEndPoint = peerEndPoint;
            this.Network = network;
            this.Behaviors = new NetworkPeerBehaviorsCollection(this);

            this.Parameters = parameters ?? new NetworkPeerConnectionParameters();
            this.MyVersion = this.Parameters.CreateVersion(this.PeerEndPoint, network, this.dateTimeProvider.GetTimeOffset());

            this.MessageReceived = new AsyncExecutionEvent<NetworkPeer, IncomingMessage>();
            this.StateChanged = new AsyncExecutionEvent<NetworkPeer, NetworkPeerState>();
        }

        /// <summary>
        /// Initializes an instance of the object for outbound network peers.
        /// </summary>
        /// <param name="peerEndPoint">IP address and port on the side of the peer.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, or <c>null</c> to use default parameters.</param>
        /// <param name="networkPeerFactory">Factory for creating P2P network peers.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public NetworkPeer(IPEndPoint peerEndPoint, Network network, NetworkPeerConnectionParameters parameters, INetworkPeerFactory networkPeerFactory, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
            : this(false, peerEndPoint, network, parameters, dateTimeProvider, loggerFactory)
        {
            TcpClient client = new TcpClient(AddressFamily.InterNetworkV6);
            client.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            client.Client.ReceiveBufferSize = parameters.ReceiveBufferSize;
            client.Client.SendBufferSize = parameters.SendBufferSize;

            this.Connection = networkPeerFactory.CreateNetworkPeerConnection(this, client, this.ProcessMessageAsync);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.Connection.Id}-{peerEndPoint}] ");
            this.logger.LogTrace("()");

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Initializes an instance of the object for inbound network peers with already established connection.
        /// </summary>
        /// <param name="peerEndPoint">IP address and port on the side of the peer.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, or <c>null</c> to use default parameters.</param>
        /// <param name="client">Already connected network client.</param>
        /// <param name="peerVersion">Version message payload received from the peer.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public NetworkPeer(IPEndPoint peerEndPoint, Network network, NetworkPeerConnectionParameters parameters, TcpClient client, IDateTimeProvider dateTimeProvider, INetworkPeerFactory networkPeerFactory, ILoggerFactory loggerFactory)
            : this(true, peerEndPoint, network, parameters, dateTimeProvider, loggerFactory)
        {
            this.Connection = networkPeerFactory.CreateNetworkPeerConnection(this, client, this.ProcessMessageAsync);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.Connection.Id}-{peerEndPoint}] ");
            this.logger.LogTrace("()");

            this.RemoteSocketEndpoint = this.PeerEndPoint;
            this.RemoteSocketAddress = this.RemoteSocketEndpoint.Address;
            this.RemoteSocketPort = this.RemoteSocketEndpoint.Port;

            this.ConnectedAt = this.dateTimeProvider.GetUtcNow();

            this.logger.LogTrace("Connected to peer '{0}'.", this.PeerEndPoint);
            this.State = NetworkPeerState.Connected;

            this.InitDefaultBehaviors(this.Parameters);
            this.Connection.StartReceiveMessages();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sets a new network state of the peer.
        /// </summary>
        /// <param name="newState">New network state to be set.</param>
        /// <remarks>This method is not thread safe.</remarks>
        public async Task SetStateAsync(NetworkPeerState newState)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(newState), newState, nameof(this.State), this.State);

            NetworkPeerState previous = this.State;
            if (previous != newState)
            {
                this.State = newState;
                await this.OnStateChangedAsync(previous).ConfigureAwait(false);

                if ((newState == NetworkPeerState.Failed) || (newState == NetworkPeerState.Offline))
                    this.logger.LogTrace("Communication with the peer has been closed.");
            }

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
                this.logger.LogTrace("Connecting to '{0}'.", this.PeerEndPoint);

                await this.Connection.ConnectAsync(this.PeerEndPoint, cancellation).ConfigureAwait(false);

                this.RemoteSocketEndpoint = this.Connection.RemoteEndPoint;
                this.RemoteSocketAddress = this.RemoteSocketEndpoint.Address;
                this.RemoteSocketPort = this.RemoteSocketEndpoint.Port;

                this.State = NetworkPeerState.Connected;
                this.ConnectedAt = this.dateTimeProvider.GetUtcNow();
                
                this.InitDefaultBehaviors(this.Parameters);
                this.Connection.StartReceiveMessages();

                this.logger.LogTrace("Outbound connection to '{0}' established.", this.PeerEndPoint);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("Connection to '{0}' cancelled.", this.PeerEndPoint);
                await this.SetStateAsync(NetworkPeerState.Offline).ConfigureAwait(false);

                this.logger.LogTrace("(-)[CANCELLED]");
                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogTrace("Exception occurred while connecting to peer '{0}': {1}", this.PeerEndPoint, ex is SocketException ? ex.Message : ex.ToString());

                this.DisconnectReason = new NetworkPeerDisconnectReason()
                {
                    Reason = "Unexpected exception while connecting to socket",
                    Exception = ex
                };

                await this.SetStateAsync(NetworkPeerState.Failed).ConfigureAwait(false);

                this.logger.LogTrace("(-)[EXCEPTION]");
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Calls event handlers when the network state of the peer is changed.
        /// </summary>
        /// <param name="previous">Previous network state of the peer.</param>
        private async Task OnStateChangedAsync(NetworkPeerState previous)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(previous), previous, nameof(this.State), this.State);

            try
            {
                await this.StateChanged.ExecuteCallbacksAsync(this, previous).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred while calling state changed callbacks: {0}", e.ToString());
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes an incoming message from the peer and calls subscribed event handlers.
        /// </summary>
        /// <param name="message">Message received from the peer.</param>
        /// <param name="cancellation">Cancellation token to abort message processing.</param>
        private async Task ProcessMessageAsync(IncomingMessage message, CancellationToken cancellation)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(message), message.Message.Command);

            try
            {
                switch (message.Message.Payload)
                {
                    case VersionPayload versionPayload:
                        await this.ProcessVersionMessageAsync(versionPayload, cancellation).ConfigureAwait(false);
                        break;

                    case HaveWitnessPayload unused:
                        this.SupportedTransactionOptions |= NetworkOptions.Witness;
                        break;
                }
            }
            catch
            {
                this.logger.LogDebug("Exception occurred while processing a message from the peer. Connection has been closed and message won't be processed further.");
                this.logger.LogTrace("(-)[EXCEPTION_PROCESSING]");
                return;
            }

            try
            {
                await this.MessageReceived.ExecuteCallbacksAsync(this, message).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                this.logger.LogCritical("Exception occurred while calling message received callbacks: {0}", e.ToString());
                this.logger.LogTrace("(-)[EXCEPTION_CALLBACKS]");
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes a "version" message received from a peer.
        /// </summary>
        /// <param name="version">Version message received from a peer.</param>
        /// <param name="cancellation">Cancellation token to abort message processing.</param>
        private async Task ProcessVersionMessageAsync(VersionPayload version, CancellationToken cancellation)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(version), version);

            this.logger.LogTrace("Peer's state is {0}.", this.State);

            switch (this.State)
            {
                case NetworkPeerState.Connected:
                    if (this.Inbound) await this.ProcessInitialVersionPayloadAsync(version, cancellation).ConfigureAwait(false);
                    break;

                case NetworkPeerState.HandShaked:
                    if (this.Version >= ProtocolVersion.REJECT_VERSION)
                    {
                        var rejectPayload = new RejectPayload()
                        {
                            Code = RejectCode.DUPLICATE
                        };

                        await this.SendMessageAsync(rejectPayload, cancellation).ConfigureAwait(false);
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
        /// <param name="cancellation">Cancellation token to abort message processing.</param>
        /// <exception cref="OperationCanceledException">Thrown if the response to our "version" message is not received on time.</exception>
        private async Task ProcessInitialVersionPayloadAsync(VersionPayload version, CancellationToken cancellation)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(version), version);

            this.PeerVersion = version;
            bool connectedToSelf = version.Nonce == this.Parameters.Nonce;

            this.logger.LogDebug("First message received from peer '{0}'.", version.AddressFrom);

            if (connectedToSelf)
            {
                this.logger.LogDebug("Connection to self detected and will be aborted.");

                VersionPayload versionPayload = this.Parameters.CreateVersion(this.PeerEndPoint, this.Network, this.dateTimeProvider.GetTimeOffset());
                await this.SendMessageAsync(versionPayload, cancellation).ConfigureAwait(false);
                this.Disconnect("Connected to self");

                this.logger.LogTrace("(-)[CONNECTED_TO_SELF]");
                return;
            }

            using (CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(this.Connection.CancellationSource.Token, cancellation))
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
        /// Exchanges "version" and "verack" messages with the peer.
        /// <para>Both parties have to send their "version" messages to the other party 
        /// as well as to acknowledge that they are happy with the other party's "version" information.</para>
        /// </summary>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <exception cref="ProtocolException">Thrown when the peer rejected our "version" message.</exception>
        /// <exception cref="OperationCanceledException">Thrown during the shutdown or when the peer disconnects.</exception>
        public async Task VersionHandshakeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.VersionHandshakeAsync(null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Exchanges "version" and "verack" messages with the peer.
        /// <para>Both parties have to send their "version" messages to the other party 
        /// as well as to acknowledge that they are happy with the other party's "version" information.</para>
        /// </summary>
        /// <param name="requirements">Protocol requirement for network peers the node wants to be connected to.</param>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <exception cref="ProtocolException">Thrown when the peer rejected our "version" message.</exception>
        /// <exception cref="OperationCanceledException">Thrown during the shutdown or when the peer disconnects.</exception>
        public async Task VersionHandshakeAsync(NetworkPeerRequirement requirements, CancellationToken cancellationToken)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(requirements), nameof(requirements.RequiredServices), requirements?.RequiredServices);

            requirements = requirements ?? new NetworkPeerRequirement();
            using (var listener = new NetworkPeerListener(this))
            {
                this.logger.LogTrace("Sending my version.");
                await this.SendMessageAsync(this.MyVersion, cancellationToken).ConfigureAwait(false);

                this.logger.LogTrace("Waiting for version or rejection message.");
                bool versionReceived = false;
                bool verAckReceived = false;
                while (!versionReceived || !verAckReceived)
                {
                    Payload payload = await listener.ReceivePayloadAsync<Payload>(cancellationToken).ConfigureAwait(false);
                    switch (payload)
                    {
                        case RejectPayload rejectPayload:
                            this.logger.LogTrace("(-)[HANDSHAKE_REJECTED]");
                            throw new ProtocolException("Handshake rejected: " + rejectPayload.Reason);

                        case VersionPayload versionPayload:
                            versionReceived = true;

                            this.PeerVersion = versionPayload;
                            if (!versionPayload.AddressReceiver.Address.Equals(this.MyVersion.AddressFrom.Address))
                            {
                                this.logger.LogDebug("Different external address detected by the node '{0}' instead of '{1}'.", versionPayload.AddressReceiver.Address, this.MyVersion.AddressFrom.Address);
                            }

                            if (versionPayload.Version < ProtocolVersion.MIN_PEER_PROTO_VERSION)
                            {
                                this.logger.LogDebug("Outdated version {0} received, disconnecting peer.", versionPayload.Version);

                                this.Disconnect("Outdated version");
                                this.logger.LogTrace("(-)[OUTDATED]");
                                return;
                            }

                            if (!requirements.Check(versionPayload))
                            {
                                this.logger.LogTrace("(-)[UNSUPPORTED_REQUIREMENTS]");
                                this.Disconnect("The peer does not support the required services requirement");
                                return;
                            }

                            this.logger.LogTrace("Sending version acknowledgement.");
                            await this.SendMessageAsync(new VerAckPayload(), cancellationToken).ConfigureAwait(false);
                            break;

                        case VerAckPayload verAckPayload:
                            verAckReceived = true;
                            break;
                    }
                }

                await this.SetStateAsync(NetworkPeerState.HandShaked).ConfigureAwait(false);

                if (this.Advertize && this.MyVersion.AddressFrom.Address.IsRoutable(true))
                {
                    var addrPayload = new AddrPayload
                    (
                        new NetworkAddress(this.MyVersion.AddressFrom)
                        {
                            Time = this.dateTimeProvider.GetTimeOffset()
                        }
                    );

                    await this.SendMessageAsync(addrPayload, cancellationToken).ConfigureAwait(false);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sends "version" message to the peer and waits for the response in form of "verack" or "reject" message.
        /// </summary>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <exception cref="ProtocolException">Thrown when the peer rejected our "version" message.</exception>
        /// <exception cref="OperationCanceledException">Thrown during the shutdown or when the peer disconnects.</exception>
        public async Task RespondToHandShakeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            using (var listener = new NetworkPeerListener(this))
            {
                this.logger.LogTrace("Responding to handshake with my version.");
                await this.SendMessageAsync(this.MyVersion, cancellationToken).ConfigureAwait(false);

                this.logger.LogTrace("Waiting for version acknowledgement or rejection message.");

                while (this.State != NetworkPeerState.HandShaked)
                {
                    Payload payload = await listener.ReceivePayloadAsync<Payload>(cancellationToken).ConfigureAwait(false);
                    switch (payload)
                    {
                        case RejectPayload rejectPayload:
                            this.logger.LogTrace("Version rejected: code {0}, reason '{1}'.", rejectPayload.Code, rejectPayload.Reason);
                            this.logger.LogTrace("(-)[VERSION_REJECTED]");
                            throw new ProtocolException("Version rejected " + rejectPayload.Code + ": " + rejectPayload.Reason);

                        case VerAckPayload verAckPayload:
                            this.logger.LogTrace("Sending version acknowledgement.");
                            await this.SendMessageAsync(new VerAckPayload(), cancellationToken).ConfigureAwait(false);
                            await this.SetStateAsync(NetworkPeerState.HandShaked).ConfigureAwait(false);
                            break;
                    }
                }
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

            if (this.IsConnected) this.SetStateAsync(NetworkPeerState.Disconnecting).GetAwaiter().GetResult();

            // We have to dispose our execution events, but we need to do that only after the Connection is fully disposed as well. 
            // Because the Connection can be disposed with another thread, the following call to dispose can return immediately 
            // and the disposing can still be in progress. Setting up the continuation task will make sure the disposing is done 
            // in correct order regardless of current state of Connection.DisposeComplete. Note that using Connection.ShutdownComplete 
            // is not enough as we especially rely on the message listener to be disposed, which is done after Connection.ShutdownComplete
            // completes.
            this.Connection.DisposeComplete.Task.ContinueWith((result) =>
            {
                this.MessageReceived.Dispose();
                this.StateChanged.Dispose();
            });

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
            return string.Format("{0} ({1})", this.State, this.PeerEndPoint);
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