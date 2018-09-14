using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusStats : SignalObserver<ChainedHeaderBlock>
    {
        private readonly CachedCoinView cache;

        private readonly DBreezeCoinView dbreeze;

        private readonly ICoinView bottom;

        private ConsensusPerformanceSnapshot lastSnapshot;

        private BackendPerformanceSnapshot lastSnapshot2;

        private CachePerformanceSnapshot lastSnapshot3;

        private readonly IConsensusManager consensusManager;
        private readonly IConsensusRuleEngine consensusRules;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        private readonly ConcurrentChain chain;

        private readonly IConnectionManager connectionManager;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        private readonly IBlockPuller blockPuller;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        public ConsensusStats(
            ICoinView coinView,
            IConsensusManager consensusManager,
            IConsensusRuleEngine consensusRules,
            IInitialBlockDownloadState initialBlockDownloadState,
            ConcurrentChain chain,
            IConnectionManager connectionManager,
            IDateTimeProvider dateTimeProvider,
            IBlockPuller blockPuller,
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime)
        {
            var stack = new CoinViewStack(coinView);
            this.cache = stack.Find<CachedCoinView>();
            this.dbreeze = stack.Find<DBreezeCoinView>();
            this.bottom = stack.Bottom;

            this.consensusManager = consensusManager;
            this.consensusRules = consensusRules;

            this.lastSnapshot = consensusRules.PerformanceCounter.Snapshot();
            this.lastSnapshot2 = this.dbreeze?.PerformanceCounter.Snapshot();
            this.lastSnapshot3 = this.cache?.PerformanceCounter.Snapshot();
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.chain = chain;
            this.connectionManager = connectionManager;
            this.dateTimeProvider = dateTimeProvider;
            this.blockPuller = blockPuller;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeLifetime = nodeLifetime;
        }

        public void BenchStats()
        {
            // TODO use NodeStats instead.
            var benchLogs = new StringBuilder();

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

        protected override void OnNextCore(ChainedHeaderBlock chainedHeaderBlock)
        {
            if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                return;

            if (this.dateTimeProvider.GetUtcNow() - this.lastSnapshot.Taken > TimeSpan.FromSeconds(5.0))
            {
                if (this.initialBlockDownloadState.IsInitialBlockDownload())
                    this.BenchStats();
            }
        }
    }
}
