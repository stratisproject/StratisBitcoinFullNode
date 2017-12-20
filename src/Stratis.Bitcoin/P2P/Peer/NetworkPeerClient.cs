﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>
    /// Represents a TCP client that can connect to a server and send and receive messages.
    /// </summary>
    public class NetworkPeerClient : IDisposable
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private ILogger logger;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Unique identifier of a client.</summary>
        public int Id { get; private set; }

        /// <summary>Underlaying TCP client.</summary>
        private TcpClient tcpClient;

        /// <summary>Prevents parallel execution of multiple write operations on <see cref="Stream"/>.</summary>
        private AsyncLock writeLock;

        /// <summary>Stream to send and receive messages through established TCP connection.</summary>
        /// <remarks>Write operations on the stream have to be protected by <see cref="writeLock"/>.</remarks>
        public NetworkStream Stream { get; private set; }

        /// <summary>Task completion that is completed when the client processing is finished.</summary>
        public TaskCompletionSource<bool> ProcessingCompletion { get; private set; }

        /// <summary><c>1</c> if the instance of the object has been disposed or disposing is in progress, <c>0</c> otherwise.</summary>
        private int disposed;

        /// <summary>Address of the end point the client is connected to, or <c>null</c> if the client has not connected yet.</summary>
        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return (IPEndPoint)this.tcpClient?.Client?.RemoteEndPoint;
            }
        }

        /// <summary>
        /// Initializes a new network client.
        /// </summary>
        /// <param name="Id">Unique identifier of a client.</param>
        /// <param name="tcpClient">Initialized TCP client, which may or may not be already connected.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public NetworkPeerClient(int Id, TcpClient tcpClient, Network network, ILoggerFactory loggerFactory)
        {
            this.tcpClient = tcpClient;

            this.loggerFactory = loggerFactory;
            this.Id = Id;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, string.Format("[{0}{1}] ", this.Id, this.RemoteEndPoint != null ? "-" + this.RemoteEndPoint.ToString() : ""));

            this.network = network;

            this.Stream = this.tcpClient.Connected ? this.tcpClient.GetStream() : null;
            this.ProcessingCompletion = new TaskCompletionSource<bool>();

            this.writeLock = new AsyncLock();
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
                await Task.Run(() => this.tcpClient.ConnectAsync(endPoint.Address, endPoint.Port).Wait(cancellation)).ConfigureAwait(false);
                this.Stream = this.tcpClient.GetStream();
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
                this.logger.LogDebug("Error connecting to '{0}', exception: {1}", endPoint, e.ToString());
                this.logger.LogTrace("(-)[UNHANDLED_EXCEPTION]");
                throw e;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sends data over the established connection.
        /// </summary>
        /// <param name="data">Data to send.</param>
        /// <param name="cancellation">Cancellation token that allows aborting the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the connection was terminated or the cancellation token was cancelled.</exception>
        public async Task SendAsync(byte[] data, CancellationToken cancellation = default(CancellationToken))
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(data), nameof(data.Length), data.Length);

            using (await this.writeLock.LockAsync(cancellation).ConfigureAwait(false))
            {
                if (this.Stream == null)
                {
                    this.logger.LogTrace("Connection has been terminated.");
                    this.logger.LogTrace("(-)[NO_STREAM]");
                    throw new OperationCanceledException();
                }

                try
                {
                    await this.Stream.WriteAsync(data, 0, data.Length, cancellation).ConfigureAwait(false);
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
        public async Task<byte[]> ReadMessageAsync(ProtocolVersion protocolVersion, CancellationToken cancellation = default(CancellationToken))
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
        public async Task ReadMagicAsync(byte[] magic, CancellationToken cancellation)
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
                int chunkSize = await this.Stream.ReadAsync(buffer, offset, bytesToRead, cancellation).ConfigureAwait(false);
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
        public async Task<Message> ReadAndParseMessageAsync(ProtocolVersion protocolVersion, CancellationToken cancellation)
        {
            this.logger.LogTrace("({0}:{1})", nameof(protocolVersion), protocolVersion);

            Message message = null;

            byte[] rawMessage = await this.ReadMessageAsync(protocolVersion, cancellation).ConfigureAwait(false);
            using (var memoryStream = new MemoryStream(rawMessage))
            {
                PerformanceCounter counter;
                message = Message.ReadNext(memoryStream, this.network, protocolVersion, cancellation, null, out counter);
                message.MessageSize = (uint)rawMessage.Length;
            }

            this.logger.LogTrace("(-):'{0}'", message);
            return message;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 1)
            {
                this.logger.LogTrace("(-)[DISPOSED]");
                return;
            }

            NetworkStream disposeStream = this.Stream;
            TcpClient disposeTcpClient = this.tcpClient;

            this.Stream = null;
            this.tcpClient = null;

            disposeStream?.Dispose();
            disposeTcpClient?.Dispose();

            this.logger.LogTrace("(-)");
        }
    }
}
