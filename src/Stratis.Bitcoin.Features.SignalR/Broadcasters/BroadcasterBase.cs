using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    /// <summary>
    /// Base class for all SignalR Broadcasters
    /// </summary>
    public abstract class ClientBroadcasterBase : IClientEventBroadcaster, IDisposable
    {
        protected readonly EventsHub eventsHub;
        private readonly INodeLifetime nodeLifetime;
        private readonly IAsyncProvider asyncProvider;
        protected readonly ILogger logger;
        private IAsyncLoop asyncLoop;

        protected ClientBroadcasterBase(
            EventsHub eventsHub,
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime,
            IAsyncProvider asyncProvider)
        {
            this.eventsHub = eventsHub;
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Init(ClientEventBroadcasterSettings broadcasterSettings)
        {
            this.logger.LogDebug($"Initialising SignalR Broadcaster {this.GetType().Name}");
            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoop(
                $"Broadcast {this.GetType().Name}",
                async token =>
                {
                    using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping, token))
                    {
                        linkedTokenSource.CancelAfter(new TimeSpan(0, 0, 10));
                        try
                        {
                            IEnumerable<IClientEvent> messages = await this.GetMessages(linkedTokenSource.Token);
                            foreach (IClientEvent clientEvent in messages)
                            {
                                await this.eventsHub.SendToClientsAsync(clientEvent);
                            }
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogError($"{this.GetType().Name} Error in GetMessages", ex);
                        }
                    }
                },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery:
                TimeSpan.FromSeconds(Math.Max(broadcasterSettings.BroadcastFrequencySeconds, 5)));

            this.OnInitialise();
        }

        protected abstract Task<IEnumerable<IClientEvent>> GetMessages(CancellationToken cancellationToken);

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            this.asyncLoop?.Dispose();
        }

        protected virtual void OnInitialise()
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}