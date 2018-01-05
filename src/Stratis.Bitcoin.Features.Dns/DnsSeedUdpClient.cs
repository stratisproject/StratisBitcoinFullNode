using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Defines a class for a UDP client that uses the <see cref="UdpClient"/> class as the underlying UDP client.
    /// </summary>
    public class DnsSeedUdpClient : IUdpClient, IDisposable
    {
        /// <summary>
        /// Defines a flag that determines if the client is disposed or not.
        /// </summary>
        private bool isDisposed = false;

        /// <summary>
        /// Defines the underlying UDP client.
        /// </summary>
        private UdpClient udpClient;

        /// <summary>
        /// Starts the UDP client to listen for incoming UDP requests.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        public void StartListening(int port)
        {
            // Create client
            if (this.udpClient != null)
            {
                // Already a client, dispose and create a new one
                this.udpClient.Dispose();
            }

            this.udpClient = new UdpClient(port);
        }

        /// <summary>
        /// Stops the UDP client.
        /// </summary>
        public void StopListening()
        {
            this.udpClient?.Dispose();
            this.udpClient = null;
        }

        /// <summary>
        /// Receives a UDP message.
        /// </summary>
        /// <returns>A task used to await the operation that returns a UDP message.</returns>
        public async Task<Tuple<IPEndPoint, byte[]>> ReceiveAsync()
        {
            this.ThrowIfDisposed();
            UdpReceiveResult result = await this.udpClient.ReceiveAsync();
            return new Tuple<IPEndPoint, byte[]>(result.RemoteEndPoint, result.Buffer);
        }

        /// <summary>
        /// Sends a UDP message.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <param name="bytes">The size of the payload.</param>
        /// <param name="remoteEndpoint">The address to send the payload.</param>
        /// <returns>A task used to await the operation.</returns>
        public async Task<int> SendAsync(byte[] payload, int bytes, IPEndPoint remoteEndpoint)
        {
            this.ThrowIfDisposed();
            return await this.udpClient.SendAsync(payload, bytes, remoteEndpoint);
        }

        /// <summary>
        /// Disposes of the client.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the client.
        /// </summary>
        /// <param name="disposing">True if deterministically disposing, otherwise False.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.udpClient?.Dispose();
                    this.udpClient = null;
                }

                this.isDisposed = true;
            }
        }

        /// <summary>
        /// Throw if object already disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(nameof(DnsSeedUdpClient));
            }
        }
    }
}
