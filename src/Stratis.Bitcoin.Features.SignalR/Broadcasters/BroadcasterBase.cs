using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    using System.Threading;

    /// <summary>
    /// Base class for all SignalR Broadcasters
    /// </summary>
    public abstract class ClientBroadcasterBase : IClientEventBroadcaster, IDisposable
    {
        private readonly EventsHub eventsHub;
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
                    using (var cancellationTokenSource = new CancellationTokenSource())
                    {
                        cancellationTokenSource.CancelAfter(new TimeSpan(0, 0, 10));

                        using (var linkedTokenSource =
                            CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, token))
                        {
                            try
                            {
                                var messages = await this.GetMessages(linkedTokenSource.Token);
                                foreach (IClientEvent clientEvent in messages)
                                {
                                    await this.eventsHub.SendToClientsAsync(clientEvent);
                                }
                            }
                            catch (Exception ex)
                            {
                                this.logger.LogError($"{this.GetType().Name} Error in GetMessages",
                                    ex);
                            }
                        }
                    }
                },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery:
                TimeSpan.FromSeconds(Math.Max(broadcasterSettings.BroadcastFrequencySeconds, 5)));
        }

        protected abstract Task<IEnumerable<IClientEvent>> GetMessages(CancellationToken cancellationToken);

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            this.asyncLoop?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}