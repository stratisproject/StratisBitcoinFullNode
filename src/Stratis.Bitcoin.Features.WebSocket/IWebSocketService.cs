using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Bitcoin.Features.WebSocket
{
    public interface IWebSocketService : IDisposable
    {
        Task<bool> StartAsync(FullNode fullNode, IEnumerable<ServiceDescriptor> services);

        Task Broadcast(string message);

        //Task<bool> SendAsync(string topic, string data);
    }
}
