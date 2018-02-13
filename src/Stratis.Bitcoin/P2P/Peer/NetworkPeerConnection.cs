using System;
using System.IO;
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
    /// Represents a network connection to a peer. It is responsible for reading incoming messages 
    /// from the peer and sending messages from the node to the peer.
    /// </summary>
    public class NetworkPeerConnection : IDisposable
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>A provider of network payload messages.</summary>
        private readonly PayloadProvider payloadProvider;

        /// <summary>Instance logger.</summary>
        private ILogger logger;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Consumer of messages coming from connected clients.</summary>
        private readonly CallbackMessageListener<IncomingMessage> messageListener;

        /// <summary>Registration to the message producer of the connected peer.</summary>
        private readonly MessageProducerRegistration<IncomingMessage> messageProducerRegistration;

        /// <summary>Unique identifier of a client.</summary>
        public int Id { get; private set; }

        /// <summary>Underlaying TCP client.</summary>
        private TcpClient tcpClient;

        /// <summary>Prevents parallel execution of multiple write operations on <see cref="stream"/>.</summary>
        private AsyncLock writeLock;

        /// <summary>Stream to send and receive messages through established TCP connection.</summary>
        /// <remarks>Write operations on the stream have to be protected by <see cref="writeLock"/>.</remarks>
        private NetworkStream stream;

        /// <summary>Address of the end point the client is connected to, or <c>null</c> if the client has not connected yet.</summary>
        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return (IPEndPoint)this.tcpClient?.Client?.RemoteEndPoint;
            }
        }

        /// <summary>Network peer this connection connects to.</summary>
        private INetworkPeer peer;

        /// <summary>Cancellation to be triggered at shutdown to abort all pending operations on the connection.</summary>
        public CancellationTokenSource CancellationSource { get; private set; }

        /// <summary>Task responsible for reading incoming messages from the stream.</summary>
        private Task receiveMessageTask;

        /// <summary>Queue of incoming messages distributed to message consumers.</summary>
        public MessageProducer<IncomingMessage> MessageProducer { get; private set; }

        /// <summary>New network state to set to the peer when shutdown is initiated.</summary>
        private NetworkPeerState setPeerStateOnShutdown;

        /// <summary>Task completion that is completed when the work of the shutdown procedure is finished, including detaching from behaviors.</summary>
        /// <seealso cref="Shutdown"/>
        /// <see cref="DisposeComplete"/>
        public TaskCompletionSource<bool> ShutdownComplete { get; private set; }

        /// <summary>Task completion that is completed when the work of <see cref="Dispose"/> is finished.</summary>
        /// <remarks>Note that first, <see cref="ShutdownComplete"/> is completed and then some more disposing 
        /// occurs in <see cref="Dispose"/>. Only when all disposing work is done, this source is completed.</remarks>
        public TaskCompletionSource<bool> DisposeComplete { get; private set; }

        /// <summary>Lock object to protect access to <see cref="shutdownInProgress"/>, <see cref="shutdownCalled"/>, <see cref="disposeRequested"/>, and <see cref="disposed"/> during the shutdown sequence.</summary>
        private readonly object shutdownLock;

        /// <summary>Set to <c>true</c> during the execution of shutdown procedure, <c>false</c> otherwise.</summary>
        /// <remarks>All access to his object has to be protected by <see cref="shutdownLock"/>.</remarks>
        /// <seealso cref="Shutdown"/>
        private bool shutdownInProgress;

        /// <summary>Set to <c>true</c> if <see cref="Shutdown"/> was called already, <c>false</c> otherwise.</summary>
        /// <remarks>All access to his object has to be protected by <see cref="shutdownLock"/>.</remarks>
        /// <seealso cref="Shutdown"/>
        private bool shutdownCalled;

        /// <summary>Set to <c>true</c> if any of the detached behavior during the shutdown procedure requested connection object disposal, <c>false</c> otherwise.</summary>
        /// <remarks>All access to his object has to be protected by <see cref="shutdownLock"/>.</remarks>
        /// <seealso cref="Shutdown"/>
        private bool disposeRequested;

        /// <summary>Set to <c>true</c> if disposal procedure has been executed, <c>false</c> otherwise.</summary>
        /// <remarks>All access to his object has to be protected by <see cref="shutdownLock"/>.</remarks>
        /// <seealso cref="Shutdown"/>
        private bool disposed;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="peer">Network peer the node is connected to, or will connect to.</param>
        /// <param name="client">Initialized TCP client, which may or may not be already connected.</param>
        /// <param name="clientId">Unique identifier of the connection.</param>
        /// <param name="processMessageAsync">Callback to be called when a new message arrives from the peer.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="payloadProvider">A provider of network payload messages.</param>
        public NetworkPeerConnection(Network network, INetworkPeer peer, TcpClient client, int clientId, ProcessMessageAsync<IncomingMessage> processMessageAsync, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, PayloadProvider payloadProvider)
        {
            this.loggerFactory = loggerFactory;
            this.payloadProvider = payloadProvider;
            this.logger = this.loggerFactory.CreateLogger(this.GetType().FullName, $"[{clientId}-{peer.PeerEndPoint}] ");

            this.network = network;
            this.dateTimeProvider = dateTimeProvider;

            this.peer = peer;
            this.setPeerStateOnShutdown = NetworkPeerState.Offline;
            this.tcpClient = client;
            this.Id = clientId;

            this.stream = this.tcpClient.Connected ? this.tcpClient.GetStream() : null;
            this.ShutdownComplete = new TaskCompletionSource<bool>();
            this.DisposeComplete = new TaskCompletionSource<bool>();

            this.shutdownLock = new object();
            this.writeLock = new AsyncLock();

            this.CancellationSource = new CancellationTokenSource();

            this.MessageProducer = new MessageProducer<IncomingMessage>();
            this.messageListener = new CallbackMessageListener<IncomingMessage>(processMessageAsync);
            this.messageProducerRegistration = this.MessageProducer.AddMessageListener(this.messageListener);
        }

        /// <summary>
        /// Starts waiting for incoming messages.
        /// </summary>
        public void StartReceiveMessages()
        {
            this.logger.LogTrace("()");

            this.receiveMessageTask = this.ReceiveMessagesAsync();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Reads messages from the connection stream.
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            this.logger.LogTrace("()");

            try
            {
                while (!this.CancellationSource.Token.IsCancellationRequested)
                {
                    Message message = await this.ReadAndParseMessageAsync(this.peer.Version, this.CancellationSource.Token).ConfigureAwait(false);

                    this.logger.LogTrace("Received message: '{0}'", message);

                    this.peer.Counter.AddRead(message.MessageSize);

                    var incomingMessage = new IncomingMessage()
                    {
                        Message = message,
                        Length = message.MessageSize,
                    };

                    this.MessageProducer.PushMessage(incomingMessage);
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

                    if (this.peer.DisconnectReason == null)
                    {
                        this.peer.DisconnectReason = new NetworkPeerDisconnectReason()
                        {
                            Reason = "Unexpected exception while waiting for a message",
                            Exception = ex
                        };
                    }

                    // We can not set the peer state directly here because it would 
                    // trigger state changing event handlers and could cause deadlock.
                    if (this.peer.State != NetworkPeerState.Offline)
                        this.setPeerStateOnShutdown = NetworkPeerState.Failed;
                }

                this.CallShutdown();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Calls <see cref="Shutdown"/> in a separated context.
        /// </summary>
        private void CallShutdown()
        {
            Task.Run(() => this.Shutdown());
        }

        /// <summary>
        /// When the connection is terminated, this method terminates the connection and informs connected behaviors about the termination.
        /// </summary>
        /// <remarks>
        /// The shutdown sequence of this object can come either from the cancellation token (which triggers the execution of this method)
        /// or from disposal of the object. Moreover, the attached behaviors which are detached here can call the disposal of the object 
        /// during the process of detachment or if they implement state changed handler.
        /// </para>
        /// <para>
        /// This is why we need to carefully control the dispose sequence in order to prevent deadlock while still making sure 
        /// the whole cleanup is done before the caller of <see cref="Dispose"/> is given the control back.
        /// <para>
        /// To achieve this, we prohibit execution of <see cref="Dispose"/> from the attached behaviors. 
        /// This is done using <see cref="shutdownInProgress"/>, which prevents execution of the disposal procedure - see the body of <see cref="Dispose"/>.
        /// </para>
        /// <para>
        /// If disposal is requested from the behavior being detached, <see cref="disposeRequested"/> is set and <see cref="Dispose"/>
        /// is called when all behaviors are detached.
        /// </para>
        /// <para>
        /// <see cref="shutdownCalled"/> protects this method from being called more than once.
        /// </para>
        /// </remarks>
        private void Shutdown()
        {
            this.logger.LogTrace("()");

            lock (this.shutdownLock)
            {
                if (this.shutdownCalled)
                {
                    this.logger.LogTrace("(-)[SHUTDOWN_CALLED]");
                    return;
                }

                this.shutdownCalled = true;
                this.shutdownInProgress = true;
            }

            this.Disconnect();

            if ((this.peer.State != NetworkPeerState.Failed) && (this.peer.State != this.setPeerStateOnShutdown))
                this.peer.SetStateAsync(this.setPeerStateOnShutdown).GetAwaiter().GetResult();

            foreach (INetworkPeerBehavior behavior in this.peer.Behaviors)
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

            this.ShutdownComplete.SetResult(true);

            bool callDispose = false;
            lock (this.shutdownLock)
            {
                this.shutdownInProgress = false;
                callDispose = this.disposeRequested;
            }

            if (callDispose) this.Dispose();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Connects the network client to the target server.
        /// </summary>
        /// <param name="endPoint">IP address and port to connect to.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the connection attempt was aborted.</exception>
        public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken cancellation)
        {
            this.logger = this.loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.Id}-{endPoint}] ");
            this.logger.LogTrace("({0}:'{1}')", nameof(endPoint), endPoint);

            try
            {
                // This variable records any error occurring in the thread pool task's context.
                Exception error = null;

                await Task.Run(() =>
                {
                    try
                    {
                        this.tcpClient.ConnectAsync(endPoint.Address, endPoint.Port).Wait(cancellation);
                    }
                    catch (Exception e)
                    {
                        // Record the error occurring in the thread pool's context.
                        error = e;
                    }
                }).ConfigureAwait(false);

                // Throw the error within this error handling context.
                if (error != null)
                    throw error;

                this.stream = this.tcpClient.GetStream();
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("Connecting to '{0}' cancelled.", endPoint);
                this.logger.LogTrace("(-)[CANCELLED]");
                throw;
            }
            catch (Exception e)
            {
                if (e is AggregateException) e = e.InnerException;
                this.logger.LogDebug("Error connecting to '{0}', exception message: {1}", endPoint, e.Message);
                this.logger.LogTrace("(-)[UNHANDLED_EXCEPTION]");
                throw e;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sends message to the connected counterparty.
        /// </summary>
        /// <param name="payload">Payload of the message to send.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the sending operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the peer has been disconnected
        /// or the cancellation token has been cancelled or another error occurred.</exception>
        public async Task SendAsync(Payload payload, CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(payload), payload);

            if (!this.payloadProvider.IsPayloadRegistered(payload.GetType()))
                throw new ProtocolViolationException($"Message payload {payload.GetType()} not found.");

            CancellationTokenSource cts = null;
            if (cancellation != default(CancellationToken))
            {
                cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, this.CancellationSource.Token);
                cancellation = cts.Token;
            }
            else cancellation = this.CancellationSource.Token;

            try
            {
                var message = new Message(this.payloadProvider)
                {
                    Magic = this.peer.Network.Magic,
                    Payload = payload
                };

                this.logger.LogTrace("Sending message: '{0}'", message);

                using (MemoryStream ms = new MemoryStream())
                {
                    message.ReadWrite(new BitcoinStream(ms, true)
                    {
                        ProtocolVersion = this.peer.Version,
                        TransactionOptions = this.peer.SupportedTransactionOptions
                    });

                    byte[] bytes = ms.ToArray();

                    await this.SendAsync(bytes, cancellation).ConfigureAwait(false);
                    this.peer.Counter.AddWritten(bytes.Length);
                }
            }
            catch (OperationCanceledException)
            {
                this.CallShutdown();
                this.logger.LogTrace("(-)[CANCELED_EXCEPTION]");
                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogTrace("Exception occurred: '{0}'", ex.ToString());

                if (this.peer.DisconnectReason == null)
                {
                    this.peer.DisconnectReason = new NetworkPeerDisconnectReason()
                    {
                        Reason = "Unexpected exception while sending a message",
                        Exception = ex
                    };
                }

                if (this.peer.State != NetworkPeerState.Offline)
                    this.setPeerStateOnShutdown = NetworkPeerState.Failed;

                this.CallShutdown();
                this.logger.LogTrace("(-)[EXCEPTION]");
                throw new OperationCanceledException();
            }
            finally
            {
                cts?.Dispose();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sends data over the established connection.
        /// </summary>
        /// <param name="data">Data to send.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the connection was terminated or the cancellation token was cancelled.</exception>
        private async Task SendAsync(byte[] data, CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(data), nameof(data.Length), data.Length);

            using (await this.writeLock.LockAsync(cancellation).ConfigureAwait(false))
            {
                if (this.stream == null)
                {
                    this.logger.LogTrace("Connection has been terminated.");
                    this.logger.LogTrace("(-)[NO_STREAM]");
                    throw new OperationCanceledException();
                }

                try
                {
                    await this.stream.WriteAsync(data, 0, data.Length, cancellation).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if ((e is IOException) || (e is OperationCanceledException))
                    {
                        this.logger.LogTrace("Connection has been terminated.");
                        if (e is IOException) this.logger.LogTrace("(-)[IO_EXCEPTION]");
                        else this.logger.LogTrace("(-)[CANCELLED]");
                        throw new OperationCanceledException();
                    }
                    else
                    {
                        this.logger.LogTrace("Exception occurred: {0}", e.ToString());
                        this.logger.LogTrace("(-)[UNHANDLED_EXCEPTION]");
                        throw;
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Reads raw message in binary form from the connection stream.
        /// </summary>
        /// <param name="protocolVersion">Version of the protocol that defines the message format.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the read operation.</param>
        /// <returns>Binary message received from the connected counterparty.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled or the end of the stream was reached.</exception>
        /// <exception cref="ProtocolViolationException">Thrown if the incoming message is too big.</exception>
        private async Task<byte[]> ReadMessageAsync(ProtocolVersion protocolVersion, CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("({0}:{1})", nameof(protocolVersion), protocolVersion);

            // First find and read the magic.
            await this.ReadMagicAsync(this.network.MagicBytes, cancellation).ConfigureAwait(false);

            // Then read the header, which is formed of command, length, and possibly also a checksum.
            int checksumSize = protocolVersion >= ProtocolVersion.MEMPOOL_GD_VERSION ? Message.ChecksumSize : 0;
            int headerSize = Message.CommandSize + Message.LengthSize + checksumSize;

            byte[] messageHeader = new byte[headerSize];
            await this.ReadBytesAsync(messageHeader, 0, headerSize, cancellation).ConfigureAwait(false);

            // Then extract the length, which is the message payload size.
            int lengthOffset = Message.CommandSize;
            uint length = BitConverter.ToUInt32(messageHeader, lengthOffset);

            // 32 MB limit on message size from Bitcoin Core.
            if (length > 0x02000000)
                throw new ProtocolViolationException("Message payload too big (over 0x02000000 bytes)");

            // Read the payload.
            int magicLength = this.network.MagicBytes.Length;
            byte[] message = new byte[magicLength + headerSize + length];

            await this.ReadBytesAsync(message, magicLength + headerSize, (int)length, cancellation).ConfigureAwait(false);

            // And copy the magic and the header to form a complete message.
            Array.Copy(this.network.MagicBytes, 0, message, 0, this.network.MagicBytes.Length);
            Array.Copy(messageHeader, 0, message, this.network.MagicBytes.Length, headerSize);

            this.logger.LogTrace("(-):*.{0}={1}", nameof(message.Length), message.Length);
            return message;
        }

        /// <summary>
        /// Seeks and reads the magic value from the connection stream.
        /// </summary>
        /// <param name="magic">Magic value that starts the message.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the read operation.</param>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled or the end of the stream was reached.</exception>
        /// <remarks>
        /// Each networkm message starts with the magic value. If the connection stream is in unknown state,
        /// the next bytes to read might not be the magic. Therefore we read from the stream until we find the magic value.
        /// </remarks>
        private async Task ReadMagicAsync(byte[] magic, CancellationToken cancellation)
        {
            this.logger.LogTrace("()");

            byte[] bytes = new byte[1];
            for (int i = 0; i < magic.Length; i++)
            {
                byte expectedByte = magic[i];

                await this.ReadBytesAsync(bytes, 0, bytes.Length, cancellation).ConfigureAwait(false);

                byte receivedByte = bytes[0];
                if (expectedByte != receivedByte)
                {
                    // If we did not receive the next byte we expected
                    // we either received the first byte of the magic value
                    // or not. If yes, we set index to 0 here, which is then
                    // incremented in for loop to 1 and we thus continue 
                    // with the second byte. Otherwise, we set index to -1 
                    // here, which means that after the loop incrementation,
                    // we will start from first byte of magic.
                    i = receivedByte == magic[0] ? 0 : -1;
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Reads a specific number of bytes from the connection stream into a buffer.
        /// </summary>
        /// <param name="buffer">Buffer to read incoming data to.</param>
        /// <param name="offset">Position in the buffer where to write the data.</param>
        /// <param name="bytesToRead">Number of bytes to read.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the read operation.</param>
        /// <returns>Binary data received from the connected counterparty.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled or the end of the stream was reached.</exception>
        private async Task ReadBytesAsync(byte[] buffer, int offset, int bytesToRead, CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(offset), offset, nameof(bytesToRead), bytesToRead);

            while (bytesToRead > 0)
            {
                int chunkSize = await this.stream.ReadAsync(buffer, offset, bytesToRead, cancellation).ConfigureAwait(false);
                if (chunkSize == 0)
                {
                    this.logger.LogTrace("(-)[STREAM_END]");
                    throw new OperationCanceledException();
                }

                offset += chunkSize;
                bytesToRead -= chunkSize;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Reads a raw binary message from the connection stream and formats it to a structured message.
        /// </summary>
        /// <param name="protocolVersion">Version of the protocol that defines the message format.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the read operation.</param>
        /// <returns>Binary message received from the connected counterparty.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled or the end of the stream was reached.</exception>
        /// <exception cref="FormatException">Thrown if the incoming message is too big.</exception>
        /// <remarks>
        /// TODO: Currently we rely on <see cref="Message.ReadNext(System.IO.Stream, Network, ProtocolVersion, CancellationToken, byte[], out PerformanceCounter)"/>
        /// for parsing the message from binary data. That method need stream to read from, so to achieve that we create a memory stream from our data,
        /// which is not efficient. This should be improved.
        /// </remarks>
        private async Task<Message> ReadAndParseMessageAsync(ProtocolVersion protocolVersion, CancellationToken cancellation)
        {
            this.logger.LogTrace("({0}:{1})", nameof(protocolVersion), protocolVersion);

            Message message = null;

            byte[] rawMessage = await this.ReadMessageAsync(protocolVersion, cancellation).ConfigureAwait(false);
            using (var memoryStream = new MemoryStream(rawMessage))
            {
                message = Message.ReadNext(memoryStream, this.network, protocolVersion, cancellation, this.payloadProvider, out PerformanceCounter counter);
                message.MessageSize = (uint)rawMessage.Length;
            }

            this.logger.LogTrace("(-):'{0}'", message);
            return message;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            bool callShutdown = false;
            lock (this.shutdownLock)
            {
                if (this.disposed)
                {
                    this.logger.LogTrace("(-)[DISPOSED]");
                    return;
                }

                if (this.shutdownInProgress)
                {
                    this.disposeRequested = true;
                    this.logger.LogTrace("(-)[SHUTDOWN_IN_PROGRESS]");
                    return;
                }

                this.disposed = true;

                callShutdown = !this.shutdownCalled;
            }

            if (callShutdown) this.CallShutdown();
            this.CancellationSource.Cancel();

            this.receiveMessageTask?.Wait();
            this.ShutdownComplete.Task.Wait();

            this.messageProducerRegistration.Dispose();
            this.messageListener.Dispose();

            this.CancellationSource.Dispose();
            this.writeLock.Dispose();

            this.DisposeComplete.SetResult(true);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        private void Disconnect()
        {
            this.logger.LogTrace("()");

            NetworkStream disposeStream = this.stream;
            TcpClient disposeTcpClient = this.tcpClient;

            this.stream = null;
            this.tcpClient = null;

            disposeStream?.Dispose();
            disposeTcpClient?.Dispose();

            this.logger.LogTrace("(-)");
        }
    }
}
