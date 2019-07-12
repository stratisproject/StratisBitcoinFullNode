using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.PoA.Events;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class EventsHub : Hub
    {
        private readonly List<SubscriptionToken> subscriptions = new List<SubscriptionToken>();
        private readonly ILogger<EventsHub> logger;

        public EventsHub(ISignals signals, ILoggerFactory loggerFactory)
        {
            this.subscriptions.Add(signals.Subscribe<BlockConnected>(this.OnEvent));
            this.subscriptions.Add(signals.Subscribe<FedMemberAdded>(this.OnEvent));
            this.subscriptions.Add(signals.Subscribe<FedMemberKicked>(this.OnEvent));

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

        private void OnEvent(EventBase @event)
        {
            this.Clients.All.SendAsync("RecieveEvent", JsonConvert.SerializeObject(@event));
        }

        // ReSharper disable once SA1202
        protected override void Dispose(bool disposing) => this.subscriptions.ForEach(s => s?.Dispose());
    }
}