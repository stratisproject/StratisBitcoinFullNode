using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class SignalRService : ISignalRService
    {
        private readonly Subject<(string topic, string data)> messageStream = new Subject<(string topic, string data)>();

        public SignalRService()
        {            
            this.MessageStream = this.messageStream.AsObservable();
        }
        
        public string ChannelPrefix { get; } = $"{Guid.NewGuid()}/";
        public IObservable<(string topic, string data)> MessageStream { get; }

        public Task PublishAsync(string topic, string data) => Task.Run(() => this.messageStream.OnNext((topic, data)));        
    }
}
