﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

// TODO add logs
// TODO save tip in BG
// TODO add tests
// initialize on DI

namespace Stratis.Bitcoin.Base
{
    public interface ITipsManager : IDisposable
    {
        void Initialize();

        void RegisterTipProvider(object provider);

        /// <summary>Provides highest tip commited between all registered components.</summary>
        ChainedHeader GetLastCommonTip();

        void CommitTipPersisted(object provider, ChainedHeader tip);
    }

    public class TipsManager : ITipsManager
    {
        private readonly IKeyValueRepository keyValueRepo;

        private const string commonTipKey = "lastcommontip";

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

        public TipsManager(IKeyValueRepository keyValueRepo, ILoggerFactory loggerFactory)
        {
            this.keyValueRepo = keyValueRepo;
            this.tipsByProvider = new Dictionary<object, ChainedHeader>();
            this.lockObject = new object();
            this.newCommonTipSetEvent = new AsyncManualResetEvent(false);
            this.cancellation = new CancellationTokenSource();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            HashHeightPair commonTipHashHeight = this.keyValueRepo.LoadValue<HashHeightPair>(commonTipKey);

            // TODO load lastCommonTip

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
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                ChainedHeader tipToSave = this.lastCommonTip;

                var hashHeight = new HashHeightPair(tipToSave);
                this.keyValueRepo.SaveValue(commonTipKey, hashHeight);
            }
        }

        /// <inheritdoc />
        public void RegisterTipProvider(object provider)
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
                    lowestTip = this.FindCommonFork(this.tipsByProvider.Values.ToList());

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
