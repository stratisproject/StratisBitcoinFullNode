using System;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;

namespace Stratis.Bitcoin.SignalR
{
    /// <summary>
    /// Hub used to communicate with clients.  Instances of this class are created and managed by SignalR, not fullnode code.
    /// </summary>
    public class SignalRHub : Hub
    {
        private readonly ISignalRService signalRService;

        public SignalRHub(ISignalRService signalRService) => this.signalRService = signalRService;

        /// <summary>
        /// A hub method automatically becomes a streaming hub method when it returns a ChannelReader<T> or a Task<ChannelReader<T>
        /// https://docs.microsoft.com/en-us/aspnet/core/signalr/streaming?view=aspnetcore-2.1
        /// </summary>
        public ChannelReader<(string topic, string data)> MessageStream()
        {
            var channel = Channel.CreateUnbounded<(string topic, string data)>();

            this.signalRService.MessageStream.Subscribe(async x => await channel.Writer.WriteAsync(x));

            return channel.Reader;
        }
    }
}