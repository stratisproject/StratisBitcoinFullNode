using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.SignalR
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class SignalRMessageArgs
    {
        public string Target { get; set; }
        public Dictionary<string, string> Args { get; set; }

        public string GetValue(string key)
        {
            return this.Args.ContainsKey(key) ? this.Args[key] : null;
        }
    }

    public class EventsHub : Hub
    {
        private readonly IDictionary<string, List<Action<SignalRMessageArgs>>> featureSubscriptions
            = new ConcurrentDictionary<string, List<Action<SignalRMessageArgs>>>(StringComparer.OrdinalIgnoreCase);

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

        /// <summary>Called using reflection from SignalR</summary> 
        // ReSharper disable once UnusedMember.Global
        public void SendMessage(SignalRMessageArgs message)
        {
            try
            {
                if (this.featureSubscriptions.ContainsKey(message.Target))
                {
                    this.featureSubscriptions[message.Target].ForEach(featureSubscription =>
                    {
                        featureSubscription.Invoke(message);
                    });
                }
            }
            catch (Exception e)
            {
                this.logger.LogError("Error SendMessage", e);
            }
        }

        public void SubscribeToIncomingSignalRMessages(string target, Action<SignalRMessageArgs> subscription)
        {
            if (!this.featureSubscriptions.ContainsKey(target))
            {
                this.featureSubscriptions.TryAdd(target, new List<Action<SignalRMessageArgs>>());
            }

            this.featureSubscriptions[target].Add(subscription);
        }

        public void UnSubscribeToIncomingSignalRMessages(string target)
        {
            if (this.featureSubscriptions.ContainsKey(target))
            {
                this.featureSubscriptions.Remove(target);
            }
        }

        public async Task SendToClientsAsync(IClientEvent @event)
        {
            // Check if any there are any connected clients
            if (this.Clients == null) return;

            try
            {
                await this.Clients.All.SendAsync("receiveEvent", @event);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error sending to clients");
            }
        }
    }
}