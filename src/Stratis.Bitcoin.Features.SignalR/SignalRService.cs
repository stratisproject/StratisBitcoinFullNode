using System;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class SignalRService : ISignalRService
    {
        public SignalRService()
        {
            this.ChannelPrefix = $"{Guid.NewGuid()}/";
        }

        /// <summary>
        /// Gets a unique string used in forming broadcast channels.  Clients can use this value when subscribing
        /// to this full node's channels.
        /// </summary>
        public string ChannelPrefix { get; }
    }
}
