using System;
using System.Net;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Defines an interface for a UDP client used to send and receive UDP messages.
    /// </summary>
    public interface IUdpClient
    {
        /// <summary>
        /// Starts the UDP client to listen for incoming UDP requests.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        /// <returns>A task used to await the operation.</returns>
        void StartListening(int port);

        /// <summary>
        /// Stops the UDP client.
        /// </summary>
        /// <returns>A task used to await the operation.</returns>
        void StopListening();

        /// <summary>
        /// Receives a UDP message.
        /// </summary>
        /// <returns>A task used to await the operation that returns a UDP message.</returns>
        Task<Tuple<IPEndPoint, byte[]>> ReceiveAsync();

        /// <summary>
        /// Sends a UDP message.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        /// <param name="bytes">The size of the payload.</param>
        /// <param name="remoteEndpoint">The address to send the payload.</param>
        /// <returns>A task used to await the operation.</returns>
        Task<int> SendAsync(byte[] payload, int bytes, IPEndPoint remoteEndpoint);
    }
}
