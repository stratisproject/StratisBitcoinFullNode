using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class TipsManagerTests : TestBase
    {
        private readonly LoggerFactory loggerFactory;
        private readonly KeyValueRepository keyValueRepo;
        private readonly ITipsManager tipsManager;

        private readonly List<ChainedHeader> mainChainHeaders;

        public TipsManagerTests() : base(KnownNetworks.StratisMain)
        {
            this.loggerFactory = new LoggerFactory();
            string dir = CreateTestDir(this);
            this.keyValueRepo = new KeyValueRepository(dir, new DBreezeSerializer(this.Network));

            this.tipsManager = new TipsManager(this.keyValueRepo, this.loggerFactory);

            this.mainChainHeaders = ChainedHeadersHelper.CreateConsecutiveHeaders(20, ChainedHeadersHelper.CreateGenesisChainedHeader(this.Network), true);
        }

        [Fact]
        public void InitializesAtGenesis()
        {
            this.tipsManager.Initialize(this.mainChainHeaders.Last());

            ChainedHeader commonTip = this.tipsManager.GetLastCommonTip();

            Assert.Equal(this.Network.GenesisHash, commonTip.HashBlock);
        }

        [Fact]
        public async Task InitializesAtLastSavedValueAsync()
        {
            this.tipsManager.Initialize(this.mainChainHeaders.Last());

            var tipProvider = new testTipProvider();
            this.tipsManager.RegisterTipProvider(tipProvider);
            this.tipsManager.CommitTipPersisted(tipProvider, this.mainChainHeaders[10]);
            Assert.Equal(this.mainChainHeaders[10], this.tipsManager.GetLastCommonTip());

            // Give it some time to save tip in bg.
            await Task.Delay(500);

            this.tipsManager.Dispose();

            var newTipsManager = new TipsManager(this.keyValueRepo, this.loggerFactory);
            newTipsManager.Initialize(this.mainChainHeaders.Last());

            Assert.Equal(this.mainChainHeaders[10], newTipsManager.GetLastCommonTip());
        }

        [Fact]
        public void CommonTipCalculatedCorrectlyWhenProvidersAreOnTheSameChain()
        {
            this.tipsManager.Initialize(this.mainChainHeaders.Last());

            var provider1 = new testTipProvider();
            var provider2 = new testTipProvider();
            var provider3 = new testTipProvider();

            this.tipsManager.RegisterTipProvider(provider1);
            this.tipsManager.RegisterTipProvider(provider2);
            this.tipsManager.RegisterTipProvider(provider3);

            this.tipsManager.CommitTipPersisted(provider1, this.mainChainHeaders[10]);
            this.tipsManager.CommitTipPersisted(provider2, this.mainChainHeaders[9]);

            // genesis is common because only 2\3 providers commited anything.
            Assert.Equal(this.mainChainHeaders[0], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider3, this.mainChainHeaders[5]);

            // 3rd provider is lowest, therefore it's tip is the common.
            Assert.Equal(this.mainChainHeaders[5], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider1, this.mainChainHeaders[2]);

            // First provider rewinded before everyone else. Now it's tip is the lowest and common.
            Assert.Equal(this.mainChainHeaders[2], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider2, this.mainChainHeaders[15]);
            this.tipsManager.CommitTipPersisted(provider3, this.mainChainHeaders[15]);

            // Nothing changes after rest of providers advance.
            Assert.Equal(this.mainChainHeaders[2], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider1, this.mainChainHeaders[14]);

            Assert.Equal(this.mainChainHeaders[14], this.tipsManager.GetLastCommonTip());
        }

        [Fact]
        public void CommonTipCalculatedCorrectlyWhenProvidersAreOnDifferentChains()
        {
            // Chain that forks at block 12
            List<ChainedHeader> altChainHeaders = ChainedHeadersHelper.CreateConsecutiveHeaders(5, this.mainChainHeaders[12]);

            this.tipsManager.Initialize(this.mainChainHeaders.Last());

            var provider1 = new testTipProvider();
            var provider2 = new testTipProvider();
            var provider3 = new testTipProvider();
            this.tipsManager.RegisterTipProvider(provider1);
            this.tipsManager.RegisterTipProvider(provider2);
            this.tipsManager.RegisterTipProvider(provider3);

            this.tipsManager.CommitTipPersisted(provider1, this.mainChainHeaders[15]);
            this.tipsManager.CommitTipPersisted(provider2, this.mainChainHeaders[15]);
            this.tipsManager.CommitTipPersisted(provider3, altChainHeaders[4]);

            Assert.Equal(this.mainChainHeaders[12], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider3, this.mainChainHeaders[18]);
            Assert.Equal(this.mainChainHeaders[15], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider1, altChainHeaders[2]);
            this.tipsManager.CommitTipPersisted(provider2, altChainHeaders[3]);
            this.tipsManager.CommitTipPersisted(provider3, altChainHeaders[4]);

            Assert.Equal(altChainHeaders[2], this.tipsManager.GetLastCommonTip());
        }

        private class testTipProvider : ITipProvider
        {
        }
    }
}
