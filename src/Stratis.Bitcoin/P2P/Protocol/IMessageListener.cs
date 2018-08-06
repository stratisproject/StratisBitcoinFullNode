namespace Stratis.Bitcoin.P2P.Protocol
{
    /// <summary>
    /// Contract for a recipient part of a consumer logic that handles incoming messages.
    /// </summary>
    /// <typeparam name="T">Type of the messages that are being handled.</typeparam>
    /// <seealso cref="Stratis.Bitcoin.P2P.Protocol.MessageProducer{T}"/>
    public interface IMessageListener<in T>
    {
        /// <summary>
        /// Handles a newly received message.
        /// </summary>
        /// <param name="message">Message to handle.</param>
        void PushMessage(T message);
    }
}