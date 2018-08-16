using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Bitcoin.SignalR
{
    public class SignalRHub : Hub
    {
        public SignalRHub(ISignalRService signalRService)
        {
            signalRService.MessageStream.Subscribe(x =>
            {
                this.Clients.All.SendAsync("onMessage", x.topic, x.data);
            });
        }

        public override System.Threading.Tasks.Task OnConnectedAsync()
        {
           
            return base.OnConnectedAsync();
        }
    }
}
