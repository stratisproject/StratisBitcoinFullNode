using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    /// <summary>Interface that every tip provider that uses <see cref="ITipsManager"/> should implement.</summary>
    public interface ITipProvider
    {
    }

    /// <summary>Component that keeps track of highest common tip between components that can have a tip.</summary>
    public interface ITipsManager : IDisposable
    {
        /// <summary>Initializes <see cref="ITipsManager"/>.</summary>
        /// <param name="highestHeader">Tip of chain of headers.</param>
        void Initialize(ChainedHeader highestHeader);

        /// <summary>Registers provider of a tip.</summary>
        /// <remarks>Common tip is selected by finding fork point between tips provided by all registered providers.</remarks>
        void RegisterTipProvider(ITipProvider provider);

        /// <summary>Provides highest tip commited between all registered components.</summary>
        ChainedHeader GetLastCommonTip();

        /// <summary>
        /// Commits persisted tip of a component.
        /// </summary>
        /// <remarks>
        /// Commiting a particular tip would mean that in case node is killed immediately component that
        /// commited such a tip would be able to recover on startup to it or any tip that is ancestor to tip commited.
        /// </remarks>
        void CommitTipPersisted(ITipProvider provider, ChainedHeader tip);
    }

    public class TipsManager : ITipsManager
    {
        private readonly IKeyValueRepository keyValueRepo;

        private const string commonTipKey = "lastcommontip";

        /// <summary>Highest commited tips mapped by their providers.</summary>
        private readonly Dictionary<ITipProvider, ChainedHeader> tipsByProvider;

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

        public TipsManager(IKeyValueRepository keyValueRepo, ILoggerFactory loggerFactory)
        {
            this.keyValueRepo = keyValueRepo;
            this.tipsByProvider = new Dictionary<ITipProvider, ChainedHeader>();
            this.lockObject = new object();
            this.newCommonTipSetEvent = new AsyncManualResetEvent(false);
            this.cancellation = new CancellationTokenSource();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void Initialize(ChainedHeader highestHeader)
        {
            if (this.commonTipPersistingTask != null)
                throw new Exception("Already initialized.");

            var commonTipHashHeight = this.keyValueRepo.LoadValue<HashHeightPair>(commonTipKey);

            if (commonTipHashHeight != null)
                this.lastCommonTip = highestHeader.FindAncestorOrSelf(commonTipHashHeight.Hash, commonTipHashHeight.Height);
            else
                // Genesis.
                this.lastCommonTip = highestHeader.GetAncestor(0);

            this.logger.LogDebug("Tips manager initialized at '{0}'.", this.lastCommonTip);

            this.commonTipPersistingTask = this.PersistCommonTipContinuouslyAsync();
        }

        /// <summary>Continuously persists <see cref="lastCommonTip"/> to hard drive.</summary>
        private async Task PersistCommonTipContinuouslyAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                try
                {
                    await this.newCommonTipSetEvent.WaitAsync(this.cancellation.Token).ConfigureAwait(false);
                    this.newCommonTipSetEvent.Reset();
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                ChainedHeader tipToSave = this.lastCommonTip;

                var hashHeight = new HashHeightPair(tipToSave);
                this.keyValueRepo.SaveValue(commonTipKey, hashHeight);

                this.logger.LogDebug("Saved common tip '{0}'.", tipToSave);
            }
        }

        /// <inheritdoc />
        public void RegisterTipProvider(ITipProvider provider)
        {
            lock (this.lockObject)
            {
                this.tipsByProvider.Add(provider, null);
            }
        }

        /// <inheritdoc />
        public ChainedHeader GetLastCommonTip()
        {
            return this.lastCommonTip;
        }

        /// <inheritdoc />
        public void CommitTipPersisted(ITipProvider provider, ChainedHeader tip)
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
                    {
                        this.logger.LogTrace("(-)[NOT_ALL_TIPS_COMMITED]");
                        return;
                    }

                    if ((lowestTip == null) || (chainedHeader.Height < lowestTip.Height))
                        lowestTip = chainedHeader;
                }

                // Last common tip can't be changed because lowest tip is equal to it already.
                if (this.lastCommonTip == lowestTip)
                {
                    this.logger.LogTrace("(-)[ALREADY_PERSISTED]");
                    return;
                }

                // Make sure all tips are on the same chain.
                bool tipsOnSameChain = true;
                foreach (ChainedHeader chainedHeader in this.tipsByProvider.Values)
                {
                    if (chainedHeader.GetAncestor(lowestTip.Height) != lowestTip)
                    {
                        tipsOnSameChain = false;
                        break;
                    }
                }

                if (!tipsOnSameChain)
                {
                    this.logger.LogDebug("Tips are not on the same chain, finding last common fork between them.");
                    lowestTip = this.FindCommonFork(this.tipsByProvider.Values.ToList());
                }

                this.lastCommonTip = lowestTip;
                this.newCommonTipSetEvent.Set();
            }
        }

        /// <summary>Finds common fork between multiple chains.</summary>
        private ChainedHeader FindCommonFork(List<ChainedHeader> tips)
        {
            ChainedHeader fork = null;

            for (int i = 1; i < tips.Count; i++)
            {
                fork = tips[i].FindFork(tips[i - 1]);
            }

            if (fork == null && tips.Count == 1)
                fork = tips[0];

            return fork;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.cancellation.Cancel();

            this.commonTipPersistingTask?.GetAwaiter().GetResult();
        }
    }
}
