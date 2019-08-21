using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    /// <summary>
    /// Base class for all SignalR Broadcasters
    /// </summary>
    public abstract class ClientBroadcasterBase : IClientEventBroadcaster
    {
        private readonly EventsHub eventsHub;
        private readonly IAsyncProvider asyncProvider;
        private readonly int broadcastFrequencySeconds;
        protected readonly ILogger logger;
        private IAsyncLoop asyncLoop;


        protected ClientBroadcasterBase(
            EventsHub eventsHub,
            ILoggerFactory loggerFactory,
            IAsyncProvider asyncProvider)
        {
            this.eventsHub = eventsHub;
            this.asyncProvider = asyncProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialise(ClientEventBroadcasterSettings broadcasterSettings)
        {
            this.logger.LogDebug($"Initialising SignalR Broadcaster {this.GetType().Name}");
            this.asyncLoop = asyncProvider.CreateAndRunAsyncLoop(
                $"Broadcast {this.GetType().Name}",
                token =>
                {
                    foreach (IClientEvent clientEvent in this.GetMessages())
                    {
                        this.eventsHub.SendToClients(clientEvent).GetAwaiter().GetResult();
                    }

                    return Task.CompletedTask;
                },
                this.asyncProvider.NodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpan.FromSeconds(Math.Max(this.broadcastFrequencySeconds, 5)));
        }

        protected abstract IEnumerable<IClientEvent> GetMessages();
    }
}