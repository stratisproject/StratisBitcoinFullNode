using System;
using System.Collections.Generic;
using System.Linq;
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
using TracerAttributes;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>
    /// State of the network connection to a peer.
    /// </summary>
    public enum NetworkPeerState : int
    {
        /// <summary>Initial state of an outbound peer.</summary>
        Created = 0,

        /// <summary>Network connection with the peer has been established.</summary>
        Connected,

        /// <summary>The node and the peer exchanged version information.</summary>
        HandShaked,

        /// <summary>Process of disconnecting the peer has been initiated.</summary>
        Disconnecting,

        /// <summary>Shutdown has been initiated, the node went offline.</summary>
        Offline,

        /// <summary>An error occurred during a network operation.</summary>
        Failed
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

        /// <summary>
        /// Checks a version payload from a peer against the requirements.
        /// </summary>
        /// <param name="version">Version payload to check.</param>
        /// <param name="reason">The reason the check failed.</param>
        /// <returns><c>true</c> if the version payload satisfies the protocol requirements, <c>false</c> otherwise.</returns>
        public virtual bool Check(VersionPayload version, out string reason)
        {
            reason = string.Empty;
            if (this.MinVersion != null)
            {
                if (version.Version < this.MinVersion.Value)
                {
                    reason = "peer version is too low";
                    return false;
                }
            }

            if ((this.RequiredServices & version.Services) != this.RequiredServices)
            {
                reason = "network service not supported";
                return false;
            }

            return true;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// All instances of this object must be disposed or disconnected. <see cref="Disconnect(string, Exception)"/> and disposing methods
    /// have the same functionality and the disconnecting method is provided only for better readability of the code.
    /// <para>It is safe to try to disconnect or dispose this object multiple times, only the first call will be processed.</para>
    /// </remarks>
    public class NetworkPeer : INetworkPeer
    {
        /// <summary>
        /// Execution context holding information about the current status of the execution
        /// in order to recognize if <see cref="NetworkPeer.onDisconnected"/> callback was requested from the same async context.
        /// </summary>
        private class DisconnectedExecutionAsyncContext
        {
            /// <summary>
            /// Set to <c>true</c> if <see cref="NetworkPeer.onDisconnected"/> was
            /// called from within the current async context, set to <c>false</c> otherwise.
            /// </summary>
            public bool DisconnectCallbackRequested { get; set; }
        }

        /// <summary>Tracker for endpoints known to be self. </summary>
        private readonly ISelfEndpointTracker selfEndpointTracker;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <inheritdoc/>
        public NetworkPeerState State { get; private set; }

        /// <summary>Table of valid transitions between peer states.</summary>
        private static readonly Dictionary<NetworkPeerState, NetworkPeerState[]> StateTransitionTable = new Dictionary<NetworkPeerState, NetworkPeerState[]>()
        {
            { NetworkPeerState.Created, new[] { NetworkPeerState.Connected, NetworkPeerState.Offline, NetworkPeerState.Failed} },
            { NetworkPeerState.Connected, new[] { NetworkPeerState.HandShaked, NetworkPeerState.Disconnecting, NetworkPeerState.Offline, NetworkPeerState.Failed} },
            { NetworkPeerState.HandShaked, new[] { NetworkPeerState.Disconnecting, NetworkPeerState.Offline, NetworkPeerState.Failed} },
            { NetworkPeerState.Disconnecting, new[] { NetworkPeerState.Offline, NetworkPeerState.Failed} },
            { NetworkPeerState.Offline, new NetworkPeerState[] {} },
            { NetworkPeerState.Failed, new NetworkPeerState[] {} }
        };

        /// <inheritdoc/>
        public IPEndPoint RemoteSocketEndpoint { get; private set; }

        /// <inheritdoc/>
        public IPAddress RemoteSocketAddress { get; private set; }

        /// <inheritdoc/>
        public int RemoteSocketPort { get; private set; }

        /// <inheritdoc/>
        public bool Inbound { get; private set; }

        /// <inheritdoc/>
        public List<INetworkPeerBehavior> Behaviors { get; private set; }

        /// <inheritdoc/>
        public IPEndPoint PeerEndPoint { get; private set; }

        /// <inheritdoc/>
        public TimeSpan? TimeOffset { get; private set; }

        /// <inheritdoc/>
        public NetworkPeerConnection Connection { get; private set; }

        /// <summary>Statistics about the number of bytes transferred from and to the peer.</summary>
        private PerformanceCounter counter;

        /// <inheritdoc/>
        public PerformanceCounter Counter
        {
            get
            {
                if (this.counter == null)
                    this.counter = new PerformanceCounter();

                return this.counter;
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public bool IsConnected
        {
            get
            {
                return (this.State == NetworkPeerState.Connected) || (this.State == NetworkPeerState.HandShaked);
            }
        }

        /// <summary><c>true</c> to advertise "addr" message with our external endpoint to the peer when passing to <see cref="NetworkPeerState.HandShaked"/> state.</summary>
        private bool advertize;

        /// <inheritdoc/>
        public VersionPayload MyVersion { get; private set; }

        /// <inheritdoc/>
        public VersionPayload PeerVersion { get; private set; }

        /// <summary>Set to <c>1</c> if the peer disconnection has been initiated, <c>0</c> otherwise.</summary>
        private int disconnected;

        /// <summary>Set to <c>1</c> if the peer disposal has been initiated, <c>0</c> otherwise.</summary>
        private int disposed;

        /// <summary>
        /// Async context to allow to recognize whether <see cref="onDisconnected"/> callback execution is scheduled in this async context.
        /// <para>
        /// It is not <c>null</c> if one of the following callbacks is in progress: <see cref="StateChanged"/>, <see cref="MessageReceived"/>,
        /// set to <c>null</c> otherwise.
        /// </para>
        /// </summary>
        private readonly AsyncLocal<DisconnectedExecutionAsyncContext> onDisconnectedAsyncContext;

        /// <summary>Transaction options we would like.</summary>
        private TransactionOptions preferredTransactionOptions;

        /// <inheritdoc/>
        public TransactionOptions SupportedTransactionOptions { get; private set; }

        /// <inheritdoc/>
        public NetworkPeerDisconnectReason DisconnectReason { get; private set; }

        /// <inheritdoc/>
        public Network Network { get; set; }

        /// <inheritdoc/>
        public AsyncExecutionEvent<INetworkPeer, NetworkPeerState> StateChanged { get; private set; }

        /// <inheritdoc/>
        public AsyncExecutionEvent<INetworkPeer, IncomingMessage> MessageReceived { get; private set; }

        /// <inheritdoc/>
        public NetworkPeerConnectionParameters ConnectionParameters { get; private set; }

        /// <inheritdoc/>
        public MessageProducer<IncomingMessage> MessageProducer { get { return this.Connection.MessageProducer; } }

        /// <summary>Callback that is invoked when peer has finished disconnecting, or <c>null</c> when no notification after the disconnection is required.</summary>
        private readonly Action<INetworkPeer> onDisconnected;

        /// <summary>Callback that is invoked just before a message is to be sent to a peer, or <c>null</c> when nothing needs to be called.</summary>
        private readonly Action<IPEndPoint, Payload> onSendingMessage;

        /// <summary>A queue for sending payload messages to peers.</summary>
        private readonly AsyncQueue<Payload> asyncQueue;

        /// <summary>
        /// Initializes parts of the object that are common for both inbound and outbound peers.
        /// </summary>
        /// <param name="inbound"><c>true</c> for inbound peers, <c>false</c> for outbound peers.</param>
        /// <param name="peerEndPoint">IP address and port on the side of the peer.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, or <c>null</c> to use default parameters.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="selfEndpointTracker">Tracker for endpoints known to be self.</param>
        /// <param name="onDisconnected">Callback that is invoked when peer has finished disconnecting, or <c>null</c> when no notification after the disconnection is required.</param>
        private NetworkPeer(bool inbound,
            IPEndPoint peerEndPoint,
            Network network,
            NetworkPeerConnectionParameters parameters,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ISelfEndpointTracker selfEndpointTracker,
            Action<INetworkPeer> onDisconnected = null,
            Action<IPEndPoint, Payload> onSendingMessage = null)
        {
            this.dateTimeProvider = dateTimeProvider;

            this.preferredTransactionOptions = parameters.PreferredTransactionOptions;
            this.SupportedTransactionOptions = parameters.PreferredTransactionOptions & ~TransactionOptions.All;

            this.State = inbound ? NetworkPeerState.Connected : NetworkPeerState.Created;
            this.Inbound = inbound;
            this.PeerEndPoint = peerEndPoint;
            this.RemoteSocketEndpoint = this.PeerEndPoint;
            this.RemoteSocketAddress = this.RemoteSocketEndpoint.Address;
            this.RemoteSocketPort = this.RemoteSocketEndpoint.Port;

            this.Network = network;
            this.Behaviors = new List<INetworkPeerBehavior>();
            this.selfEndpointTracker = selfEndpointTracker;

            this.onDisconnectedAsyncContext = new AsyncLocal<DisconnectedExecutionAsyncContext>();

            this.ConnectionParameters = parameters ?? new NetworkPeerConnectionParameters();
            this.MyVersion = this.ConnectionParameters.CreateVersion(this.selfEndpointTracker.MyExternalAddress, this.PeerEndPoint, network, this.dateTimeProvider.GetTimeOffset());

            this.MessageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();
            this.StateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            this.onDisconnected = onDisconnected;
            this.onSendingMessage = onSendingMessage;

            this.asyncQueue = new AsyncQueue<Payload>(this.SendMessageHandledAsync);
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
        /// <param name="selfEndpointTracker">Tracker for endpoints known to be self.</param>
        /// <param name="onDisconnected">Callback that is invoked when peer has finished disconnecting, or <c>null</c> when no notification after the disconnection is required.</param>
        public NetworkPeer(IPEndPoint peerEndPoint,
            Network network,
            NetworkPeerConnectionParameters parameters,
            INetworkPeerFactory networkPeerFactory,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ISelfEndpointTracker selfEndpointTracker,
            Action<INetworkPeer> onDisconnected = null,
            Action<IPEndPoint, Payload> onSendingMessage = null)
            : this(false, peerEndPoint, network, parameters, dateTimeProvider, loggerFactory, selfEndpointTracker, onDisconnected, onSendingMessage)
        {
            var client = new TcpClient(AddressFamily.InterNetworkV6);
            client.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            client.Client.ReceiveBufferSize = parameters.ReceiveBufferSize;
            client.Client.SendBufferSize = parameters.SendBufferSize;

            this.Connection = networkPeerFactory.CreateNetworkPeerConnection(this, client, this.ProcessMessageAsync);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.Connection.Id}-{peerEndPoint}] ");
        }

        /// <summary>
        /// Initializes an instance of the object for inbound network peers with already established connection.
        /// </summary>
        /// <param name="peerEndPoint">IP address and port on the side of the peer.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, or <c>null</c> to use default parameters.</param>
        /// <param name="client">Already connected network client.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="networkPeerFactory">Factory for creating P2P network peers.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="selfEndpointTracker">Tracker for endpoints known to be self.</param>
        /// <param name="onDisconnected">Callback that is invoked when peer has finished disconnecting, or <c>null</c> when no notification after the disconnection is required.</param>
        public NetworkPeer(IPEndPoint peerEndPoint,
            Network network,
            NetworkPeerConnectionParameters parameters,
            TcpClient client,
            IDateTimeProvider dateTimeProvider,
            INetworkPeerFactory networkPeerFactory,
            ILoggerFactory loggerFactory,
            ISelfEndpointTracker selfEndpointTracker,
            Action<INetworkPeer> onDisconnected = null,
            Action<IPEndPoint, Payload> onSendingMessage = null)
            : this(true, peerEndPoint, network, parameters, dateTimeProvider, loggerFactory, selfEndpointTracker, onDisconnected, onSendingMessage)
        {
            this.Connection = networkPeerFactory.CreateNetworkPeerConnection(this, client, this.ProcessMessageAsync);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.Connection.Id}-{peerEndPoint}] ");

            this.logger.LogTrace("Connected to peer '{0}'.", this.PeerEndPoint);

            this.InitDefaultBehaviors(this.ConnectionParameters);
            this.Connection.StartReceiveMessages();
        }

        /// <summary>
        /// Sets a new network state of the peer.
        /// </summary>
        /// <param name="newState">New network state to be set.</param>
        /// <remarks>This method is not thread safe.</remarks>
        private async Task SetStateAsync(NetworkPeerState newState)
        {
            NetworkPeerState previous = this.State;

            if (StateTransitionTable[previous].Contains(newState))
            {
                this.State = newState;

                await this.OnStateChangedAsync(previous).ConfigureAwait(false);

                if ((newState == NetworkPeerState.Failed) || (newState == NetworkPeerState.Offline))
                {
                    this.logger.LogTrace("Communication with the peer has been closed.");

                    this.ExecuteDisconnectedCallbackWhenSafe();
                }
            }
            else if (previous != newState)
            {
                this.logger.LogDebug("Illegal transition from {0} to {1} occurred.", previous, newState);
            }
        }

        /// <inheritdoc/>
        public async Task ConnectAsync(CancellationToken cancellation = default(CancellationToken))
        {
            try
            {
                this.logger.LogTrace("Connecting to '{0}'.", this.PeerEndPoint);

                await this.Connection.ConnectAsync(this.PeerEndPoint, cancellation).ConfigureAwait(false);

                this.RemoteSocketEndpoint = this.Connection.RemoteEndPoint;
                this.RemoteSocketAddress = this.RemoteSocketEndpoint.Address;
                this.RemoteSocketPort = this.RemoteSocketEndpoint.Port;

                this.State = NetworkPeerState.Connected;

                this.InitDefaultBehaviors(this.ConnectionParameters);
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
        }

        /// <summary>
        /// Calls event handlers when the network state of the peer is changed.
        /// </summary>
        /// <param name="previous">Previous network state of the peer.</param>
        private async Task OnStateChangedAsync(NetworkPeerState previous)
        {
            bool insideCallback = this.onDisconnectedAsyncContext.Value == null;
            if (!insideCallback)
                this.onDisconnectedAsyncContext.Value = new DisconnectedExecutionAsyncContext();

            try
            {
                await this.StateChanged.ExecuteCallbacksAsync(this, previous).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred while calling state changed callbacks: {0}", e.ToString());
                throw;
            }
            finally
            {
                if (!insideCallback)
                {
                    if (this.onDisconnectedAsyncContext.Value.DisconnectCallbackRequested)
                        this.onDisconnected(this);

                    this.onDisconnectedAsyncContext.Value = null;
                }
            }
        }

        /// <summary>
        /// Processes an incoming message from the peer and calls subscribed event handlers.
        /// </summary>
        /// <param name="message">Message received from the peer.</param>
        /// <param name="cancellation">Cancellation token to abort message processing.</param>
        private async Task ProcessMessageAsync(IncomingMessage message, CancellationToken cancellation)
        {
            try
            {
                switch (message.Message.Payload)
                {
                    case VersionPayload versionPayload:
                        await this.ProcessVersionMessageAsync(versionPayload, cancellation).ConfigureAwait(false);
                        break;

                    case HaveWitnessPayload unused:
                        this.SupportedTransactionOptions |= TransactionOptions.Witness;
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
                this.onDisconnectedAsyncContext.Value = new DisconnectedExecutionAsyncContext();

                await this.MessageReceived.ExecuteCallbacksAsync(this, message).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                this.logger.LogCritical("Exception occurred while calling message received callbacks: {0}", e.ToString());
                this.logger.LogTrace("(-)[EXCEPTION_CALLBACKS]");
                throw;
            }
            finally
            {
                if (this.onDisconnectedAsyncContext.Value.DisconnectCallbackRequested)
                    this.onDisconnected(this);

                this.onDisconnectedAsyncContext.Value = null;
            }
        }

        /// <summary>
        /// Processes a "version" message received from a peer.
        /// </summary>
        /// <param name="version">Version message received from a peer.</param>
        /// <param name="cancellation">Cancellation token to abort message processing.</param>
        private async Task ProcessVersionMessageAsync(VersionPayload version, CancellationToken cancellation)
        {
            this.logger.LogTrace("Peer's state is {0}.", this.State);

            switch (this.State)
            {
                case NetworkPeerState.Connected:
                    if (this.Inbound)
                        await this.ProcessInitialVersionPayloadAsync(version, cancellation).ConfigureAwait(false);

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
                this.SupportedTransactionOptions |= TransactionOptions.Witness;
        }

        /// <summary>
        /// Processes an initial "version" message received from a peer.
        /// </summary>
        /// <param name="version">Version message received from a peer.</param>
        /// <param name="cancellation">Cancellation token to abort message processing.</param>
        /// <exception cref="OperationCanceledException">Thrown if the response to our "version" message is not received on time.</exception>
        private async Task ProcessInitialVersionPayloadAsync(VersionPayload version, CancellationToken cancellation)
        {
            this.PeerVersion = version;
            bool connectedToSelf = version.Nonce == this.ConnectionParameters.Nonce;

            this.logger.LogDebug("First message received from peer '{0}'.", version.AddressFrom);

            if (connectedToSelf)
            {
                this.logger.LogDebug("Connection to self detected, disconnecting.");

                this.Disconnect("Connected to self");
                this.selfEndpointTracker.Add(version.AddressReceiver);

                this.logger.LogTrace("(-)[CONNECTED_TO_SELF]");
                throw new OperationCanceledException();
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
        }

        /// <summary>
        /// Initializes behaviors from the default template.
        /// </summary>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, including the default behaviors template.</param>
        private void InitDefaultBehaviors(NetworkPeerConnectionParameters parameters)
        {
            this.advertize = parameters.Advertize;
            this.preferredTransactionOptions = parameters.PreferredTransactionOptions;

            foreach (INetworkPeerBehavior behavior in parameters.TemplateBehaviors)
            {
                this.Behaviors.Add(behavior.Clone());
            }

            if ((this.State == NetworkPeerState.Connected) || (this.State == NetworkPeerState.HandShaked))
            {
                foreach (INetworkPeerBehavior behavior in this.Behaviors)
                {
                    behavior.Attach(this);
                }
            }
        }

        /// <inheritdoc/>
        public void SendMessage(Payload payload)
        {
            Guard.NotNull(payload, nameof(payload));

            if (!this.IsConnected)
            {
                this.logger.LogTrace("(-)[NOT_CONNECTED]");
                throw new OperationCanceledException("The peer has been disconnected");
            }

            this.asyncQueue.Enqueue(payload);
        }

        /// <summary>
        /// This is used by the asyncQueue to send payloads messages to peers under a separate thread.
        /// If a message is sent inside the state change even and the send fails this could cause a deadlock,
        /// to avoid that if there is any danger of a deadlock it better to use the SendMessage method and go via the queue.
        /// </summary>
        private async Task SendMessageHandledAsync(Payload payload, CancellationToken cancellation = default(CancellationToken))
        {
            try
            {
                await this.SendMessageAsync(payload, cancellation);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("Connection to '{0}' cancelled.", this.PeerEndPoint);
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred while connecting to peer '{0}': {1}", this.PeerEndPoint, ex is SocketException ? ex.Message : ex.ToString());
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task SendMessageAsync(Payload payload, CancellationToken cancellation = default(CancellationToken))
        {
            Guard.NotNull(payload, nameof(payload));

            if (!this.IsConnected)
            {
                this.logger.LogTrace("(-)[NOT_CONNECTED]");
                throw new OperationCanceledException("The peer has been disconnected");
            }

            this.onSendingMessage?.Invoke(this.RemoteSocketEndpoint, payload);

            await this.Connection.SendAsync(payload, cancellation).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task VersionHandshakeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.VersionHandshakeAsync(null, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task VersionHandshakeAsync(NetworkPeerRequirement requirements, CancellationToken cancellationToken)
        {
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

                            if (!requirements.Check(versionPayload, out string reason))
                            {
                                this.logger.LogTrace("(-)[UNSUPPORTED_REQUIREMENTS]");
                                this.Disconnect("The peer does not support the required services requirement, reason: " + reason);
                                return;
                            }

                            this.logger.LogTrace("Sending version acknowledgement.");
                            await this.SendMessageAsync(new VerAckPayload(), cancellationToken).ConfigureAwait(false);
                            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(versionPayload.AddressFrom, false);
                            break;

                        case VerAckPayload verAckPayload:
                            verAckReceived = true;
                            break;
                    }
                }

                await this.SetStateAsync(NetworkPeerState.HandShaked).ConfigureAwait(false);

                if (this.advertize && this.MyVersion.AddressFrom.Address.IsRoutable(true))
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
        }

        /// <inheritdoc/>
        public async Task RespondToHandShakeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
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
        }

        /// <inheritdoc/>
        public void Disconnect(string reason, Exception exception = null)
        {
            if (Interlocked.CompareExchange(ref this.disconnected, 1, 0) == 1)
            {
                this.logger.LogTrace("(-)[DISCONNECTED]");
                return;
            }

            if (this.IsConnected) this.SetStateAsync(NetworkPeerState.Disconnecting).GetAwaiter().GetResult();

            this.Connection.Disconnect();

            if (this.DisconnectReason == null)
            {
                this.DisconnectReason = new NetworkPeerDisconnectReason()
                {
                    Reason = reason,
                    Exception = exception
                };
            }

            NetworkPeerState newState = exception == null ? NetworkPeerState.Offline : NetworkPeerState.Failed;
            this.SetStateAsync(newState).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Executes <see cref="onDisconnected"/> callback if no callbacks are currently executing in the same async context,
        /// schedules <see cref="onDisconnected"/> execution after the callback otherwise.
        /// </summary>
        private void ExecuteDisconnectedCallbackWhenSafe()
        {
            if (this.onDisconnected != null)
            {
                // Value wasn't set in this async context, which means that we are outside of the callbacks execution and it is allowed to call `onDisconnected`.
                if (this.onDisconnectedAsyncContext.Value == null)
                {
                    this.logger.LogTrace("Disconnection callback is being executed.");
                    this.onDisconnected(this);
                }
                else
                {
                    this.logger.LogTrace("Disconnection callback is scheduled for execution when other callbacks are finished.");
                    this.onDisconnectedAsyncContext.Value.DisconnectCallbackRequested = true;
                }
            }
            else
                this.logger.LogTrace("Disconnection callback is not specified.");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 1)
            {
                this.logger.LogTrace("(-)[DISPOSED]");
                return;
            }

            this.Disconnect("Peer disposed");

            this.logger.LogTrace("Behaviors detachment started.");

            foreach (INetworkPeerBehavior behavior in this.Behaviors)
            {
                try
                {
                    behavior.Detach();
                    behavior.Dispose();
                }
                catch (Exception ex)
                {
                    this.logger.LogError("Error while detaching behavior '{0}': {1}", behavior.GetType().FullName, ex.ToString());
                }
            }

            this.asyncQueue.Dispose();
            this.Connection.Dispose();

            this.MessageReceived.Dispose();
            this.StateChanged.Dispose();
        }

        /// <inheritdoc />
        public InventoryType AddSupportedOptions(InventoryType inventoryType)
        {
            // Transaction options we prefer and which are also supported by peer.
            TransactionOptions actualTransactionOptions = this.preferredTransactionOptions & this.SupportedTransactionOptions;

            if ((actualTransactionOptions & TransactionOptions.Witness) != 0)
                inventoryType |= InventoryType.MSG_WITNESS_FLAG;

            return inventoryType;
        }

        /// <inheritdoc />
        [NoTrace]
        public T Behavior<T>() where T : INetworkPeerBehavior
        {
            return this.Behaviors.OfType<T>().FirstOrDefault();
        }
    }
}