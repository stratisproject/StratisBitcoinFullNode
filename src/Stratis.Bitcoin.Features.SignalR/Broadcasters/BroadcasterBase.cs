namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Stratis.Bitcoin.AsyncWork;
    using Stratis.Bitcoin.Signals;
    using Stratis.Bitcoin.Utilities;

    public abstract class ClientBroadcasterBase : IClientEventBroadcaster
    {
        private readonly EventsHub eventsHub;
        private readonly INodeLifetime nodeLifetime;
        private readonly ILoggerFactory loggerFactory;
        private readonly AsyncProvider asyncProvider;
        private readonly int broadcastFrequencySeconds;
        protected readonly ILogger logger;
        private IAsyncLoop asyncLoop;


        protected ClientBroadcasterBase(
            EventsHub eventsHub,
            ISignals signals,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            int broadcastFrequencySeconds = 5)
        {
            this.eventsHub = eventsHub;
            this.nodeLifetime = nodeLifetime;
            this.loggerFactory = loggerFactory;
            this.broadcastFrequencySeconds = broadcastFrequencySeconds;
            this.asyncProvider = new AsyncProvider(this.loggerFactory, signals, new NodeLifetime());
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialise()
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
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpan.FromSeconds(this.broadcastFrequencySeconds));
        }

        protected abstract IEnumerable<IClientEvent> GetMessages();
    }
}