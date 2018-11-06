using System;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.SignalR
{
    /// <summary>
    /// Interface to allow for SignalR broadcast communication.
    /// </summary>
    public interface ISignalRService : IDisposable
    {
        bool Started { get;  }

        /// <summary>
        /// The hosting address of the SignalR server.
        /// </summary>
        /// <remarks>
        /// This value is required by clients of this service when establishing a connection.
        /// </remarks>
        Uri Address { get; }

        /// <summary>
        /// The address of the hub used by the service.
        /// </summary>
        Uri HubRoute { get; }

        /// <summary>
        /// Stream of messages of topic & data, sent to this service through SendAsync.
        /// </summary>
        /// <remarks>
        /// Subscribed to by the SignalRHub in order to receive messages for broadcast.
        /// </remarks>
        IObservable<(string topic, string data)> MessageStream { get; }

        /// <summary>
        /// Starts the service asynchronously.
        /// </summary>
        /// <returns>
        /// Task-true if started successfully, otherwise Task-false.
        /// </returns>
        Task<bool> StartAsync();

        /// <summary>
        /// Sends string data and associated an topic for broadcast.
        /// </summary>
        /// <param name="topic"> Allows clients to determine source of data. </param>
        /// <param name="data"> The data to be broadcast. </param>
        /// <returns>
        /// Task-true if sent successfully, otherwise Task-false.
        /// </returns>
        Task<bool> SendAsync(string topic, string data);

        /// <summary>
        /// Event fired once the service has started up.
        /// </summary>
        /// <returns>
        /// The absolute URI of the hub that has been started.
        /// </returns>
        IObservable<string> StartedStream { get; }
    }
}