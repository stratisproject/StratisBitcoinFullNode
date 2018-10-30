using System;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoA.MockChain
{
    public class PoAMockChainFixture : IDisposable
    {
        public PoAMockChain Chain { get; }

        public PoAMockChainFixture()
        {
            this.Chain = new PoAMockChain(2);
            var node1 = this.Chain.Nodes[0];
            var node2 = this.Chain.Nodes[1];

            // node1 gets premine
            TestHelper.WaitLoop(() => node1.CoreNode.GetTip().Height >= this.Chain.Network.Consensus.PremineHeight + this.Chain.Network.Consensus.CoinbaseMaturity + 1);
            this.Chain.WaitForAllNodesToSync();
            Assert.Equal(this.Chain.Network.Consensus.PremineReward.Satoshi, (long)node1.WalletSpendableBalance);

            // node2 gets a big payout from node1
            int currentHeight = node1.CoreNode.GetTip().Height;
            node1.SendTransaction(node2.MinerAddress.ScriptPubKey,new Money(this.Chain.Network.Consensus.PremineReward.Satoshi / 2, MoneyUnit.Satoshi));
            TestHelper.WaitLoop(() => node1.CoreNode.GetTip().Height >= currentHeight + 1);
            this.Chain.WaitForAllNodesToSync();
        }

        public void Dispose()
        {
            this.Chain.Dispose();
        }
    }
}
