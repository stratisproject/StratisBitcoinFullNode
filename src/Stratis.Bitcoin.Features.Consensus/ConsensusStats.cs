using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusStats : SignalObserver<Block>
    {
        private readonly ICachedCoinView cache;

        private readonly ICoinViewStorage dbreeze;

        private ConsensusPerformanceSnapshot lastSnapshot;

        private BackendPerformanceSnapshot lastSnapshot2;

        private CachePerformanceSnapshot lastSnapshot3;
        private readonly IConsensusRuleEngine consensusRules;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        private readonly IBlockPuller blockPuller;

        public ConsensusStats(
            ICachedCoinView cachedCoinView,
            ICoinViewStorage coinViewStorage,
            IConsensusRuleEngine consensusRules,
            IInitialBlockDownloadState initialBlockDownloadState,
            IDateTimeProvider dateTimeProvider,
            IBlockPuller blockPuller,
            ILoggerFactory loggerFactory)
        {
            this.cache = cachedCoinView;
            this.dbreeze = coinViewStorage;

            this.consensusRules = consensusRules;

            this.lastSnapshot = consensusRules.PerformanceCounter.Snapshot();
            this.lastSnapshot2 = this.dbreeze?.PerformanceCounter.Snapshot();
            this.lastSnapshot3 = this.cache?.PerformanceCounter.Snapshot();
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.dateTimeProvider = dateTimeProvider;
            this.blockPuller = blockPuller;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void BenchStats()
        {
            var benchLogs = new StringBuilder();

            this.blockPuller.ShowStats(benchLogs);

            if (this.cache != null)
                benchLogs.AppendLine("Cache entries".PadRight(LoggingConfiguration.ColumnLength) + this.cache.CacheEntryCount);

            ConsensusPerformanceSnapshot snapshot = this.consensusRules.PerformanceCounter.Snapshot();
            benchLogs.AppendLine((snapshot - this.lastSnapshot).ToString());
            this.lastSnapshot = snapshot;

            if (this.dbreeze != null)
            {
                BackendPerformanceSnapshot snapshot2 = this.dbreeze.PerformanceCounter.Snapshot();
                benchLogs.AppendLine((snapshot2 - this.lastSnapshot2).ToString());
                this.lastSnapshot2 = snapshot2;
            }

            if (this.cache != null)
            {
                CachePerformanceSnapshot snapshot3 = this.cache.PerformanceCounter.Snapshot();
                benchLogs.AppendLine((snapshot3 - this.lastSnapshot3).ToString());
                this.lastSnapshot3 = snapshot3;
            }

            this.logger.LogInformation(benchLogs.ToString());
        }

        protected override void OnNextCore(Block value)
        {
            if (this.dateTimeProvider.GetUtcNow() - this.lastSnapshot.Taken > TimeSpan.FromSeconds(5.0))
            {
                if (this.initialBlockDownloadState.IsInitialBlockDownload())
                    this.BenchStats();
            }
        }
    }
}
