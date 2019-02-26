using System;
using NBitcoin;

namespace Stratis.SmartContracts.Tests.Common.MockChain
{
    public class PoAMockChainFixture : IMockChainFixture, IDisposable
    {
        public IMockChain Chain { get; }

        public PoAMockChainFixture()
        {
            PoAMockChain mockChain = new PoAMockChain(2).Build();
            this.Chain = mockChain;
            MockChainNode node1 = this.Chain.Nodes[0];
            MockChainNode node2 = this.Chain.Nodes[1];

            // Get premine
            mockChain.MineBlocks(10);

            // Send half to other from whoever received premine
            if ((long)node1.WalletSpendableBalance == node1.CoreNode.FullNode.Network.Consensus.PremineReward.Satoshi)
            {
                this.PayHalfPremine(node1, node2);
            }
            else
            {
                this.PayHalfPremine(node2, node1);
            }
        }

        private void PayHalfPremine(MockChainNode from, MockChainNode to)
        {
            from.SendTransaction(to.MinerAddress.ScriptPubKey, new Money(from.CoreNode.FullNode.Network.Consensus.PremineReward.Satoshi / 2, MoneyUnit.Satoshi));
            from.WaitMempoolCount(1);
            this.Chain.MineBlocks(1);
        }

        public void Dispose()
        {
            this.Chain.Dispose();
        }
    }
}
