﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    public class NetworkPeerServer : IDisposable
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; private set; }

        /// <summary>Version of the protocol that the server is running.</summary>
        public ProtocolVersion Version { get; private set; }

        /// <summary>The parameters that will be cloned and applied for each peer connecting to <see cref="NetworkPeerServer"/>.</summary>
        public NetworkPeerConnectionParameters InboundNetworkPeerConnectionParameters { get; set; }

        /// <summary><c>true</c> to allow connections from LAN, <c>false</c> otherwise.</summary>
        public bool AllowLocalPeers { get; set; }

        /// <summary>Maximal number of inbound connection that the server is willing to handle simultaneously.</summary>
        public int MaxConnections { get; set; }

        /// <summary>IP address and port, on which the server listens to incoming connections.</summary>
        private IPEndPoint localEndpoint;
        /// <summary>IP address and port, on which the server listens to incoming connections.</summary>
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

        /// <summary>Network socket that the server listens on and accept new connections with.</summary>
        private Socket socket;

        /// <summary>Queue of incoming messages distributed to message consumers.</summary>
        private readonly MessageProducer<IncomingMessage> messageProducer = new MessageProducer<IncomingMessage>();

        /// <summary>IP address and port of the external network interface that is accessible from the Internet.</summary>
        volatile IPEndPoint externalEndpoint;
        /// <summary>IP address and port of the external network interface that is accessible from the Internet.</summary>
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

        /// <summary>List of network client peers that are currently connected to the server.</summary>
        public NetworkPeerCollection ConnectedNetworkPeers { get; private set; }

        /// <summary>Cancellation that is triggered on shutdown to stop all pending operations.</summary>
        private CancellationTokenSource serverCancel = new CancellationTokenSource();

        /// <summary>Nonce for server's version payload.</summary>
        private ulong nonce;
        /// <summary>Nonce for server's version payload.</summary>
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

        /// <summary>Consumer of messages coming from connected clients.</summary>
        /// <seealso cref="ProcessMessage(IncomingMessage)"/>
        private readonly EventLoopMessageListener<IncomingMessage> listener;

        /// <summary>
        /// Initializes instance of a network peer server.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="version">Version of the network protocol that the server should run.</param>
        /// <param name="internalPort">Port on which the server will listen, or -1 to use the default port for the selected network.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="networkPeerFactory">Factory for creating P2P network peers.</param>
        public NetworkPeerServer(Network network, ProtocolVersion version, int internalPort, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INetworkPeerFactory networkPeerFactory)
        {
            internalPort = internalPort == -1 ? network.DefaultPort : internalPort;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{internalPort}] ");
            this.logger.LogTrace("({0}:{1},{2}:{3},{4}:{5})", nameof(network), network, nameof(version), version, nameof(internalPort), internalPort);

            this.dateTimeProvider = dateTimeProvider;
            this.networkPeerFactory = networkPeerFactory;

            this.AllowLocalPeers = true;
            this.InboundNetworkPeerConnectionParameters = new NetworkPeerConnectionParameters();

            this.localEndpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0").MapToIPv6Ex(), internalPort);

            this.MaxConnections = 125;
            this.Network = network;
            this.externalEndpoint = new IPEndPoint(this.localEndpoint.Address, this.Network.DefaultPort);
            this.Version = version;

            this.listener = new EventLoopMessageListener<IncomingMessage>(ProcessMessage);
            this.messageProducer = new MessageProducer<IncomingMessage>();
            this.messageProducer.AddMessageListener(this.listener);

            this.ConnectedNetworkPeers = new NetworkPeerCollection();
            this.ConnectedNetworkPeers.MessageProducer.AddMessageListener(this.listener);

            this.logger.LogTrace("Network peer server ready to listen on '{0}'.", this.LocalEndpoint);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Starts listening on the server's initialialized endpoint.
        /// </summary>
        /// <param name="maxIncoming">Maximal number of newly connected clients waiting to be accepted.</param>
        public void Listen(int maxIncoming = 8)
        {
            this.logger.LogTrace("({0}:{1})", nameof(maxIncoming), maxIncoming);

            if (this.socket != null)
            {
                this.logger.LogTrace("(-)[ALREADY_LISTENING]");
                throw new InvalidOperationException("Already listening");
            }

            try
            {
                this.socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                this.socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

                this.socket.Bind(this.LocalEndpoint);
                this.socket.Listen(maxIncoming);

                this.logger.LogTrace("Listening started.");
                this.BeginAccept();
            }
            catch (Exception ex)
            {
                this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Starts accepting connections from newly connected clients.
        /// </summary>
        private void BeginAccept()
        {
            this.logger.LogTrace("()");

            if (this.serverCancel.IsCancellationRequested)
            {
                this.logger.LogTrace("Stop accepting connection.");
                return;
            }

            this.logger.LogTrace("Accepting incoming connections.");

            var args = new SocketAsyncEventArgs();
            args.Completed += Accept_Completed;
            if (!this.socket.AcceptAsync(args))
                this.EndAccept(args);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Callback that is called when a new connection is accepted.
        /// </summary>
        /// <inheritdoc/>
        private void Accept_Completed(object sender, SocketAsyncEventArgs e)
        {
            this.logger.LogTrace("()");

            this.EndAccept(e);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Handles a completed accept connection event and starts accepting again.
        /// </summary>
        /// <param name="args">Information about the accept connection event.</param>
        private void EndAccept(SocketAsyncEventArgs args)
        {
            this.logger.LogTrace("()");

            Socket client = null;
            try
            {
                if (args.SocketError != SocketError.Success)
                    throw new SocketException((int)args.SocketError);

                client = args.AcceptSocket;
                if (this.serverCancel.IsCancellationRequested)
                {
                    this.logger.LogTrace("(-)[CANCELLED]");
                    return;
                }

                this.logger.LogTrace("Connection accepted from client '{0}'.", client.RemoteEndPoint);
                using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(this.serverCancel.Token))
                {
                    cancel.CancelAfter(TimeSpan.FromSeconds(10));

                    var stream = new NetworkStream(client, false);
                    while (true)
                    {
                        if (this.ConnectedNetworkPeers.Count >= this.MaxConnections)
                        {
                            this.logger.LogDebug("Maximum number of connections {0} reached.", this.MaxConnections);
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
                            NetworkPeer = null,
                        });

                        if (message.Payload is VersionPayload)
                            break;

                        this.logger.LogTrace("The first message of the remote peer '{0}' did not contain a version payload.", client.RemoteEndPoint);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (!this.serverCancel.Token.IsCancellationRequested)
                    this.logger.LogTrace("Inbound client '{0}' failed to send a message within 10 seconds, dropping connection.", client.RemoteEndPoint);

                Utils.SafeCloseSocket(client);
            }
            catch (Exception ex)
            {
                if (this.serverCancel.IsCancellationRequested)
                    return;

                if (client == null)
                {
                    this.logger.LogTrace("Exception occurred while accepting connection: {0}", ex.ToString());
                    Thread.Sleep(3000);
                }
                else
                {
                    this.logger.LogTrace("Exception occurred while processing message from client '{0}': {1}", client.RemoteEndPoint, ex.ToString());
                    Utils.SafeCloseSocket(client);
                }
            }

            this.BeginAccept();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Callback that is called when a new message is received from a connected client peer.
        /// </summary>
        /// <param name="message">Message received from the client.</param>
        private void ProcessMessage(IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(message), message.Message.Command);

            this.ProcessMessageCore(message);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes a new message received from a connected client peer.
        /// </summary>
        /// <param name="message">Message received from the client.</param>
        private void ProcessMessageCore(IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(message), message.Message.Command);

            if (message.Message.Payload is VersionPayload)
            {
                VersionPayload version = message.AssertPayload<VersionPayload>();
                bool connectedToSelf = version.Nonce == this.Nonce;

                if (connectedToSelf) this.logger.LogDebug("Connection to self detected and will be aborted.");

                if ((message.NetworkPeer != null) && connectedToSelf)
                {
                    message.NetworkPeer.DisconnectAsync();

                    this.logger.LogTrace("(-)[CONNECTED_TO_SELF]");
                    return;
                }

                if (message.NetworkPeer == null)
                {
                    IPEndPoint remoteEndpoint = version.AddressFrom;
                    if (!remoteEndpoint.Address.IsRoutable(this.AllowLocalPeers))
                    {
                        // Send his own endpoint.
                        remoteEndpoint = new IPEndPoint(((IPEndPoint)message.Socket.RemoteEndPoint).Address, this.Network.DefaultPort);
                    }

                    var peerAddress = new NetworkAddress()
                    {
                        Endpoint = remoteEndpoint,
                        Time = this.dateTimeProvider.GetUtcNow()
                    };

                    NetworkPeer networkPeer = this.networkPeerFactory.CreateNetworkPeer(peerAddress, this.Network, CreateNetworkPeerConnectionParameters(), message.Socket, version);
                    if (connectedToSelf)
                    {
                        networkPeer.SendMessage(CreateNetworkPeerConnectionParameters().CreateVersion(networkPeer.PeerAddress.Endpoint, this.Network, this.dateTimeProvider.GetTimeOffset()));
                        networkPeer.Disconnect();

                        this.logger.LogTrace("(-)[CONNECTED_TO_SELF_2]");
                        return;
                    }

                    using (CancellationTokenSource cancel = new CancellationTokenSource())
                    {
                        cancel.CancelAfter(TimeSpan.FromSeconds(10.0));
                        try
                        {
                            this.ConnectedNetworkPeers.Add(networkPeer);
                            networkPeer.StateChanged += Peer_StateChanged;
                            networkPeer.RespondToHandShake(cancel.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            this.logger.LogTrace("Remote peer haven't responded within 10 seconds of the handshake completion, dropping connection.");

                            networkPeer.DisconnectAsync();

                            this.logger.LogTrace("(-)[HANDSHAKE_TIMEDOUT]");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogTrace("Exception occurred: {0}", ex.ToString());

                            networkPeer.DisconnectAsync();

                            this.logger.LogTrace("(-)[HANDSHAKE_EXCEPTION]");
                            throw;
                        }
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Callback that is called when a network status of a connected client peer changes.
        /// <para>The peer is removed from the list of connected peers if the connection has been terminated for any reason.</para>
        /// </summary>
        /// <param name="peer">The connected peer.</param>
        /// <param name="oldState">Previous state of the peer. New state of the peer is stored in its <see cref="NetworkPeer.State"/> property.</param>
        private void Peer_StateChanged(NetworkPeer peer, NetworkPeerState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}.{5}:{6})", nameof(peer), peer.PeerAddress, nameof(oldState), oldState, nameof(peer), nameof(peer.State), peer.State);

            if ((peer.State == NetworkPeerState.Disconnecting)
                || (peer.State == NetworkPeerState.Failed)
                || (peer.State == NetworkPeerState.Offline))
                this.ConnectedNetworkPeers.Remove(peer);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            if (!this.serverCancel.IsCancellationRequested)
            {
                this.serverCancel.Cancel();

                this.logger.LogTrace("Stopping network peer server.");
                this.listener.Dispose();

                try
                {
                    this.ConnectedNetworkPeers.DisconnectAll();
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

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Initializes connection parameters using the server's initialized values.
        /// </summary>
        /// <returns>Initialized connection parameters.</returns>
        private NetworkPeerConnectionParameters CreateNetworkPeerConnectionParameters()
        {
            IPEndPoint myExternal = Utils.EnsureIPv6(this.ExternalEndpoint);
            NetworkPeerConnectionParameters param2 = this.InboundNetworkPeerConnectionParameters.Clone();
            param2.Nonce = this.Nonce;
            param2.Version = this.Version;
            param2.AddressFrom = myExternal;
            return param2;
        }
    }
}