using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

// TODO create common storage for the data
// TODO add logs
// TODO save tip in BG
// TODO add tests

namespace Stratis.Bitcoin.Base
{
    public class TipsManager : IDisposable
    {
        /// <summary>Highest commited tips mapped by their providers.</summary>
        private readonly Dictionary<object, ChainedHeader> tipsByProvider;

        /// <summary>Highest tip commited between all registered components.</summary>
        private ChainedHeader lastCommonTip;

        /// <summary>Protects all access to <see cref="tipsByProvider"/> and write access to <see cref="lastCommonTip"/>.</summary>
        private readonly object lockObject;

        /// <summary>Triggered when <see cref="lastCommonTip"/> is updated.</summary>
        private readonly AsyncManualResetEvent newCommonTipSetEvent;

        private Task commonTipPersistingTask;

        private readonly CancellationTokenSource cancellation;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public TipsManager(ILoggerFactory loggerFactory)
        {
            this.tipsByProvider = new Dictionary<object, ChainedHeader>();
            this.lockObject = new object();
            this.newCommonTipSetEvent = new AsyncManualResetEvent(false);
            this.cancellation = new CancellationTokenSource();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            // TODO load lastCommonTip

            this.commonTipPersistingTask = this.PersistCommonTipContinuously();
        }

        /// <summary>Continuously persists <see cref="lastCommonTip"/> to hard drive.</summary>
        private async Task PersistCommonTipContinuously()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                try
                {
                    await this.newCommonTipSetEvent.WaitAsync(this.cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                ChainedHeader tipToSave = this.lastCommonTip;

                // TODO save to db
            }
        }

        // Type is the type of component that requires syncing the tips.
        public void RegisterTipProvider(object provider)
        {
            lock (this.lockObject)
            {
                this.tipsByProvider.Add(provider, null);
            }
        }

        /// <summary>Provides highest tip commited between all registered components.</summary>
        public ChainedHeader GetLastCommonTip()
        {
            return this.lastCommonTip;
        }

        // throws if provider was not registered
        public void CommitTipPersisted(object provider, ChainedHeader tip)
        {
            lock (this.lockObject)
            {
                this.tipsByProvider[provider] = tip;

                // Get lowest tip out of all tips commited.
                ChainedHeader lowestTip = null;
                foreach (ChainedHeader chainedHeader in this.tipsByProvider.Values)
                {
                    // Do nothing if there is at least 1 component that didn't commit it's tip yet.
                    if (chainedHeader == null)
                        return;

                    if ((lowestTip == null) || (chainedHeader.Height < lowestTip.Height))
                        lowestTip = chainedHeader;
                }

                // Last common tip can't be changed because lowest tip is equal to it already.
                if (this.lastCommonTip == lowestTip)
                    return;

                // Make sure all tips are on the same chain.
                foreach (ChainedHeader chainedHeader in this.tipsByProvider.Values)
                {
                    if (chainedHeader.GetAncestor(lowestTip.Height) != lowestTip)
                    {
                        // Not all tips are on the same chain.
                        // Wait till all components finish reorging.
                        return;
                    }
                }

                this.lastCommonTip = lowestTip;
                this.newCommonTipSetEvent.Set();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.cancellation.Cancel();

            this.commonTipPersistingTask?.GetAwaiter().GetResult();
        }
    }
}
