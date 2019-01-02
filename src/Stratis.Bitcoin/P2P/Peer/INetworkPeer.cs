using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>
    /// Represents a counterparty of the node on the network. This is usually another node, but it can be
    /// a wallet, an analytical robot, or any other network client or server that understands the protocol.
    /// <para>The network peer is connected either inbound, if it was the counterparty that established
    /// the connection to our node's listener, or outbound, if our node was the one connecting to a remote server.
    /// </para>
    /// </summary>
    public interface INetworkPeer : IDisposable
    {
        /// <summary>State of the network connection to the peer.</summary>
        NetworkPeerState State { get; }

        /// <summary>IP address and port of the connected peer.</summary>
        IPEndPoint RemoteSocketEndpoint { get; }

        /// <summary>IP address part of <see cref="RemoteSocketEndpoint"/>.</summary>
        IPAddress RemoteSocketAddress { get; }

        /// <summary>Port part of <see cref="RemoteSocketEndpoint"/>.</summary>
        int RemoteSocketPort { get; }

        /// <summary><c>true</c> if the peer connected to the node, <c>false</c> if the node connected to the peer.</summary>
        bool Inbound { get; }

        /// <summary>List of node's modules attached to the peer to receive notifications about various events related to the peer.</summary>
        List<INetworkPeerBehavior> Behaviors { get; }

        /// <summary>IP address and port on the side of the peer.</summary>
        IPEndPoint PeerEndPoint { get; }

        /// <summary>Difference between the local clock and the clock that peer claims, or <c>null</c> if this information has not been initialized yet.</summary>
        TimeSpan? TimeOffset { get; }

        /// <summary>Component representing the network connection to the peer that is responsible for sending and receiving messages.</summary>
        NetworkPeerConnection Connection { get; }

        /// <summary>Statistics about the number of bytes transferred from and to the peer.</summary>
        PerformanceCounter Counter { get; }

        /// <summary>
        /// The negotiated protocol version (minimum of supported version between <see cref="MyVersion"/> and the <see cref="PeerVersion"/>).
        /// </summary>
        ProtocolVersion Version { get; }

        /// <summary><c>true</c> if the connection to the peer is considered active, <c>false</c> otherwise, including any case of error.</summary>
        bool IsConnected { get; }

        /// <summary>Node's version message payload that is sent to the peer.</summary>
        VersionPayload MyVersion { get; }

        /// <summary>Version message payload received from the peer.</summary>
        VersionPayload PeerVersion { get; }

        /// <summary>Transaction options supported by the peer.</summary>
        TransactionOptions SupportedTransactionOptions { get; }

        /// <summary>When a peer is disconnected this is set to human readable information about why it happened.</summary>
        NetworkPeerDisconnectReason DisconnectReason { get; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        Network Network { get; }

        /// <summary>Event that is triggered when the peer's network state is changed.</summary>
        /// <remarks>Do not dispose the peer from this callback.</remarks>
        AsyncExecutionEvent<INetworkPeer, NetworkPeerState> StateChanged { get; }

        /// <summary>Event that is triggered when a new message is received from a network peer.</summary>
        /// <remarks>Do not dispose the peer from this callback.</remarks>
        AsyncExecutionEvent<INetworkPeer, IncomingMessage> MessageReceived { get; }

        /// <summary>Various settings and requirements related to how the connections with peers are going to be established.</summary>
        NetworkPeerConnectionParameters ConnectionParameters { get; }

        /// <summary>Queue of the connections' incoming messages distributed to message consumers.</summary>
        MessageProducer<IncomingMessage> MessageProducer { get; }

        /// <summary>
        /// Connects the node to an outbound peer using already initialized information about the peer and starts receiving messages in a separate task.
        /// </summary>
        /// <param name="cancellation">Cancellation that allows aborting establishing the connection with the peer.</param>
        /// <exception cref="OperationCanceledException">Thrown when the cancellation token has been cancelled.</exception>
        Task ConnectAsync(CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Send a message by putting it in a send queue.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <exception cref="OperationCanceledException">Thrown when the peer has been disconnected or the cancellation token has been cancelled.</exception>
        void SendMessage(Payload payload);

        /// <summary>
        /// Send a message to the peer asynchronously.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the sending operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the peer has been disconnected or the cancellation token has been cancelled.</exception>
        Task SendMessageAsync(Payload payload, CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Exchanges "version" and "verack" messages with the peer.
        /// <para>Both parties have to send their "version" messages to the other party
        /// as well as to acknowledge that they are happy with the other party's "version" information.</para>
        /// </summary>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <exception cref="ProtocolException">Thrown when the peer rejected our "version" message.</exception>
        /// <exception cref="OperationCanceledException">Thrown during the shutdown or when the peer disconnects.</exception>
        Task VersionHandshakeAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Exchanges "version" and "verack" messages with the peer.
        /// <para>Both parties have to send their "version" messages to the other party
        /// as well as to acknowledge that they are happy with the other party's "version" information.</para>
        /// </summary>
        /// <param name="requirements">Protocol requirement for network peers the node wants to be connected to.</param>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <exception cref="ProtocolException">Thrown when the peer rejected our "version" message.</exception>
        /// <exception cref="OperationCanceledException">Thrown during the shutdown or when the peer disconnects.</exception>
        Task VersionHandshakeAsync(NetworkPeerRequirement requirements, CancellationToken cancellationToken);

        /// <summary>
        /// Sends "version" message to the peer and waits for the response in form of "verack" or "reject" message.
        /// </summary>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <exception cref="ProtocolException">Thrown when the peer rejected our "version" message.</exception>
        /// <exception cref="OperationCanceledException">Thrown during the shutdown or when the peer disconnects.</exception>
        Task RespondToHandShakeAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Disconnects the peer and cleans up.
        /// </summary>
        /// <param name="reason">Human readable reason for disconnecting.</param>
        /// <param name="exception">Exception because of which the disconnection happened, or <c>null</c> if there were no exception.</param>
        void Disconnect(string reason, Exception exception = null);

        /// <summary>
        /// Add supported option to the inventory type.
        /// </summary>
        /// <param name="inventoryType">Inventory type to extend.</param>
        /// <returns>Inventory type possibly extended with new options.</returns>
        InventoryType AddSupportedOptions(InventoryType inventoryType);

        /// <summary>
        /// Finds all behaviors of a specific behavior type among the peer's behaviors.
        /// </summary>
        /// <typeparam name="T">Type of the behavior to find.</typeparam>
        /// <returns>Collection of behaviors of specific type.</returns>
        T Behavior<T>() where T : INetworkPeerBehavior;
    }
}