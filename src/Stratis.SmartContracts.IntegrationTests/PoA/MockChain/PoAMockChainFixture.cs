using System;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.SmartContracts.IntegrationTests.MockChain;
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

            // Send half to other from whoever received premine
            if ((long)node1.WalletSpendableBalance == this.Chain.Network.Consensus.PremineReward.Satoshi)
            {
                PayHalfPremine(node1, node2);
            }
            else
            {
                PayHalfPremine(node2, node1);
            }
        }

        private void PayHalfPremine(MockChainNode from, MockChainNode to)
        {
            int currentHeight = from.CoreNode.GetTip().Height;
            from.SendTransaction(to.MinerAddress.ScriptPubKey, new Money(this.Chain.Network.Consensus.PremineReward.Satoshi / 2, MoneyUnit.Satoshi));
            TestHelper.WaitLoop(() => from.CoreNode.GetTip().Height >= currentHeight + 1);
            this.Chain.WaitForAllNodesToSync();
        }

        public void Dispose()
        {
            this.Chain.Dispose();
        }
    }
}
