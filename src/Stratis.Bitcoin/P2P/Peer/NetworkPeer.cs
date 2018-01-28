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

    /// <inheritdoc/>
    /// <remarks>
    /// All instances of this object must be disposed or disconnected. <see cref="Disconnect(string, Exception)"/> and disposing methods 
    /// have the same functionality and the disconnecting method is provided only for better readability of the code. 
    /// <para>It is safe to try to disconnect or dispose this object multiple times, only the first call will be processed.</para>
    /// </remarks>
    public class NetworkPeer : INetworkPeer
    {
        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <inheritdoc/>
        public NetworkPeerState State { get; private set; }

        /// <inheritdoc/>
        public IPEndPoint RemoteSocketEndpoint { get; private set; }

        /// <inheritdoc/>
        public IPAddress RemoteSocketAddress { get; private set; }

        /// <inheritdoc/>
        public int RemoteSocketPort { get; private set; }

        /// <inheritdoc/>
        public bool Inbound { get; private set; }

        /// <inheritdoc/>
        public NetworkPeerBehaviorsCollection Behaviors { get; private set; }

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

        /// <summary>Transaction options we would like.</summary>
        private NetworkOptions preferredTransactionOptions;

        /// <inheritdoc/>
        public NetworkOptions SupportedTransactionOptions { get; private set; }

        /// <inheritdoc/>
        public NetworkPeerDisconnectReason DisconnectReason { get; set; }

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
            this.PeerEndPoint = peerEndPoint;
            this.Network = network;
            this.Behaviors = new NetworkPeerBehaviorsCollection(this);

            this.ConnectionParameters = parameters ?? new NetworkPeerConnectionParameters();
            this.MyVersion = this.ConnectionParameters.CreateVersion(this.PeerEndPoint, network, this.dateTimeProvider.GetTimeOffset());

            this.MessageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();
            this.StateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
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

            this.logger.LogTrace("Connected to peer '{0}'.", this.PeerEndPoint);
            this.State = NetworkPeerState.Connected;

            this.InitDefaultBehaviors(this.ConnectionParameters);
            this.Connection.StartReceiveMessages();

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
            bool connectedToSelf = version.Nonce == this.ConnectionParameters.Nonce;

            this.logger.LogDebug("First message received from peer '{0}'.", version.AddressFrom);

            if (connectedToSelf)
            {
                this.logger.LogDebug("Connection to self detected, disconnecting.");

                this.Disconnect("Connected to self");

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

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Initializes behaviors from the default template.
        /// </summary>
        /// <param name="parameters">Various settings and requirements related to how the connections with peers are going to be established, including the default behaviors template.</param>
        private void InitDefaultBehaviors(NetworkPeerConnectionParameters parameters)
        {
            this.logger.LogTrace("()");

            this.advertize = parameters.Advertize;
            this.preferredTransactionOptions = parameters.PreferredTransactionOptions;

            this.Behaviors.DelayAttach = true;
            foreach (INetworkPeerBehavior behavior in parameters.TemplateBehaviors)
            {
                this.Behaviors.Add(behavior.Clone());
            }

            this.Behaviors.DelayAttach = false;

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task VersionHandshakeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.VersionHandshakeAsync(null, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
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

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void Disconnect(string reason, Exception exception = null)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(reason), reason);

            if (Interlocked.CompareExchange(ref this.disconnected, 1, 0) == 1)
            {
                this.logger.LogTrace("(-)[DISCONNECTED]");
                return;
            }

            if (this.IsConnected) this.SetStateAsync(NetworkPeerState.Disconnecting).GetAwaiter().GetResult();

            if (this.DisconnectReason == null)
            {
                this.DisconnectReason = new NetworkPeerDisconnectReason()
                {
                    Reason = reason,
                    Exception = exception
                };
            }

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
            }, TaskContinuationOptions.ExecuteSynchronously);

            this.Connection.Dispose();

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public InventoryType AddSupportedOptions(InventoryType inventoryType)
        {
            // Transaction options we prefer and which are also supported by peer.
            NetworkOptions actualTransactionOptions = this.preferredTransactionOptions & this.SupportedTransactionOptions;

            if ((actualTransactionOptions & NetworkOptions.Witness) != 0)
                inventoryType |= InventoryType.MSG_WITNESS_FLAG;

            return inventoryType;
        }

        /// <inheritdoc />
        public T Behavior<T>() where T : NetworkPeerBehavior
        {
            return this.Behaviors.Find<T>();
        }

        /// <inheritdoc />
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