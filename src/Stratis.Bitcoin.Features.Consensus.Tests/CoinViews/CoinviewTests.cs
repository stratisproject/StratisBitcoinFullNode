using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.CoinViews
{
    public class CoinviewTests
    {
        private readonly Network network;
        private readonly DataFolder dataFolder;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILoggerFactory loggerFactory;
        private readonly INodeStats nodeStats;
        private readonly DBreezeCoinView dbreezeCoinview;

        private readonly ConcurrentChain concurrentChain;
        private readonly StakeChainStore stakeChainStore;
        private readonly IRewindDataIndexCache rewindDataIndexCache;
        private readonly CachedCoinView cachedCoinView;

        public CoinviewTests()
        {
            this.network = new StratisMain();
            this.dataFolder = TestBase.CreateDataFolder(this);
            this.dateTimeProvider = new DateTimeProvider();
            this.loggerFactory = new ExtendedLoggerFactory();
            this.nodeStats = new NodeStats(this.dateTimeProvider);

            this.dbreezeCoinview = new DBreezeCoinView(this.network, this.dataFolder, this.dateTimeProvider, this.loggerFactory, this.nodeStats, new DBreezeSerializer(this.network));
            this.dbreezeCoinview.InitializeAsync().GetAwaiter().GetResult();

            this.concurrentChain = new ConcurrentChain(this.network);
            this.stakeChainStore = new StakeChainStore(this.network, this.concurrentChain, this.dbreezeCoinview, this.loggerFactory);
            this.stakeChainStore.LoadAsync().GetAwaiter().GetResult();

            this.rewindDataIndexCache = new RewindDataIndexCache(this.dateTimeProvider, this.network);

            this.cachedCoinView = new CachedCoinView(this.dbreezeCoinview, this.dateTimeProvider, this.loggerFactory, this.nodeStats, this.stakeChainStore, this.rewindDataIndexCache);

            this.rewindDataIndexCache.InitializeAsync(this.concurrentChain.Height, this.cachedCoinView);
        }

        [Fact]
        public async Task DoSmth()
        {
            uint256 tip = await this.cachedCoinView.GetTipHashAsync();
            Assert.Equal(this.concurrentChain.Tip.HashBlock, tip);
        }
    }
}
