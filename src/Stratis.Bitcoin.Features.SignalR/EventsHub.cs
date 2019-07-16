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

        public async Task SendToClients(EventBase @event)
        {
            try
            {
                string data = JsonConvert.SerializeObject(new
                {
                    @event.CorrelationId
                });
                await this.Clients.All.SendAsync("recieveEvent", data);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error sending to clients");
            }
        }
    }
}