using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using System;
using System.Text;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusStats
    {
        private CoinViewStack stack;
        private CachedCoinView cache;
        private DBreezeCoinView dbreeze;
        private CoinView bottom;

        private LookaheadBlockPuller lookaheadPuller;
        private ConsensusPerformanceSnapshot lastSnapshot;
        private BackendPerformanceSnapshot lastSnapshot2;
        private CachePerformanceSnapshot lastSnapshot3;
        private readonly ConsensusLoop consensusLoop;
        private readonly ChainBehavior.ChainState chainState;
        private readonly ConcurrentChain chain;
        private readonly IConnectionManager connectionManager;

        public ConsensusStats(CoinViewStack stack, CoinView coinView, ConsensusLoop consensusLoop, ChainBehavior.ChainState chainState, ConcurrentChain chain, IConnectionManager connectionManager)
        {
            stack = new CoinViewStack(coinView);
            cache = stack.Find<CachedCoinView>();
            dbreeze = stack.Find<DBreezeCoinView>();
            bottom = stack.Bottom;

            this.consensusLoop = consensusLoop;
            lookaheadPuller = this.consensusLoop.Puller as LookaheadBlockPuller;

            lastSnapshot = consensusLoop.Validator.PerformanceCounter.Snapshot();
            lastSnapshot2 = dbreeze?.PerformanceCounter.Snapshot();
            lastSnapshot3 = cache?.PerformanceCounter.Snapshot();
            this.chainState = chainState;
            this.chain = chain;
            this.connectionManager = connectionManager;
        }

        public bool CanLog
        {
            get
            {
                return this.chainState.IsInitialBlockDownload && (DateTimeOffset.UtcNow - lastSnapshot.Taken) > TimeSpan.FromSeconds(5.0);
            }
        }

        public void Log()
        {
            StringBuilder benchLogs = new StringBuilder();

            if (lookaheadPuller != null)
            {
                benchLogs.AppendLine("======Block Puller======");
                benchLogs.AppendLine("Lookahead:".PadRight(Logs.ColumnLength) + lookaheadPuller.ActualLookahead + " blocks");
                benchLogs.AppendLine("Downloaded:".PadRight(Logs.ColumnLength) + lookaheadPuller.MedianDownloadCount + " blocks");
                benchLogs.AppendLine("==========================");
            }
            benchLogs.AppendLine("Persistent Tip:".PadRight(Logs.ColumnLength) + this.chain.GetBlock(bottom.GetBlockHashAsync().Result).Height);
            if (cache != null)
            {
                benchLogs.AppendLine("Cache Tip".PadRight(Logs.ColumnLength) + this.chain.GetBlock(cache.GetBlockHashAsync().Result).Height);
                benchLogs.AppendLine("Cache entries".PadRight(Logs.ColumnLength) + cache.CacheEntryCount);
            }

            var snapshot = this.consensusLoop.Validator.PerformanceCounter.Snapshot();
            benchLogs.AppendLine((snapshot - lastSnapshot).ToString());
            lastSnapshot = snapshot;

            if (dbreeze != null)
            {
                var snapshot2 = dbreeze.PerformanceCounter.Snapshot();
                benchLogs.AppendLine((snapshot2 - lastSnapshot2).ToString());
                lastSnapshot2 = snapshot2;
            }
            if (cache != null)
            {
                var snapshot3 = cache.PerformanceCounter.Snapshot();
                benchLogs.AppendLine((snapshot3 - lastSnapshot3).ToString());
                lastSnapshot3 = snapshot3;
            }
            benchLogs.AppendLine(this.connectionManager.GetStats());
            Logs.Bench.LogInformation(benchLogs.ToString());
        }
    }
}
