using System;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.SignalR
{
    public interface ISignalRService
    {
        Uri Address { get; }

        IObservable<(string topic, string data)> MessageStream { get; }

        Task<bool> StartAsync();

        Task PublishAsync(string topic, string data);
    }
}
