using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class EventsHub : Hub
    {
        private readonly ILogger<EventsHub> logger;

        public EventsHub(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<EventsHub>();
        }

        public override Task OnConnectedAsync()
        {
            this.logger.LogDebug("New client with id {id} connected", this.Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            this.logger.LogDebug("Client with id {id} disconnected", this.Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public void SendToClients(EventBase @event)
        {
            this.Clients.All.SendAsync("RecieveEvent", JsonConvert.SerializeObject(@event));
        }
    }
}