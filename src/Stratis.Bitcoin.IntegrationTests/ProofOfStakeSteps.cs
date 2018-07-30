using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ProofOfStakeSteps
    {
        private IDictionary<string, CoreNode> nodes;
        public readonly NodeGroupBuilder NodeGroupBuilder;
        private readonly SharedSteps sharedSteps;

        public readonly string PremineNode = "PremineNode";
        public readonly string PremineWallet = "preminewallet";
        public readonly string PremineWalletAccount = "account 0";
        public readonly string PremineWalletPassword = "preminewalletpassword";

        public ProofOfStakeSteps(string displayName)
        {
            this.sharedSteps = new SharedSteps();
            this.NodeGroupBuilder = new NodeGroupBuilder(Path.Combine(this.GetType().Name, displayName));
        }

        public void GenerateCoins()
        {
            PremineNodeWithWallet();
            MineGenesisAndPremineBlocks();
            MineCoinsToMaturity();
            PremineNodeMinesTenBlocksMoreEnsuringTheyCanBeStaked();
            PremineNodeStartsStaking();
            PremineNodeWalletHasEarnedCoinsThroughStaking();
        }

        public CoreNode PremineNodeWithCoins => this.nodes?[this.PremineNode];

        public void PremineNodeWithWallet()
        {
            this.nodes = this.NodeGroupBuilder
                    .CreateStratisPosNode(this.PremineNode)
                    .Start()
                    .NotInIBD()
                    .WithWallet(this.PremineWallet, this.PremineWalletPassword)
                    .Build();
        }

        public void MineGenesisAndPremineBlocks()
        {
            this.sharedSteps.MinePremineBlocks(this.nodes[this.PremineNode], this.PremineWallet, this.PremineWalletAccount, this.PremineWalletPassword);
        }

        public void MineCoinsToMaturity()
        {
            this.nodes[this.PremineNode].GenerateStratisWithMiner(100);
            this.sharedSteps.WaitForNodeToSync(this.nodes[this.PremineNode]);
        }

        public void PremineNodeMinesTenBlocksMoreEnsuringTheyCanBeStaked()
        {
            this.nodes[this.PremineNode].GenerateStratisWithMiner(Convert.ToInt32(this.nodes[this.PremineNode].FullNode.Network.Consensus.CoinbaseMaturity));
        }

        public void PremineNodeStartsStaking()
        {
            var minter = this.nodes[this.PremineNode].FullNode.NodeService<IPosMinting>();
            minter.Stake(new WalletSecret() { WalletName = PremineWallet, WalletPassword = PremineWalletPassword });
        }

        public void PremineNodeWalletHasEarnedCoinsThroughStaking()
        {
            var network = this.nodes[this.PremineNode].FullNode.Network;
            var premine = network.Consensus.ProofOfWorkReward + network.Consensus.PremineReward;
            var mineToMaturity = network.Consensus.ProofOfWorkReward * 110;
            var balanceShouldBe = premine + mineToMaturity;
            TestHelper.WaitLoop(() =>
            {
                long staked = this.nodes[this.PremineNode].FullNode.WalletManager().GetSpendableTransactionsInWallet(this.PremineWallet).Sum(s => s.Transaction.Amount);
                return staked > balanceShouldBe;
            });
        }
    }
}