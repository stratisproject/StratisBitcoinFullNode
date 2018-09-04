using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ProofOfStakeSteps
    {
        private IDictionary<string, CoreNode> nodes;
        public readonly NodeGroupBuilder NodeGroupBuilder;

        public readonly string PremineNode = "PremineNode";
        public readonly string PremineWallet = "preminewallet";
        public readonly string PremineWalletAccount = "account 0";
        public readonly string PremineWalletPassword = "preminewalletpassword";
        public readonly string PremineWalletPassphrase = "";

        private readonly HashSet<uint256> transactionsBeforeStaking = new HashSet<uint256>();

        public ProofOfStakeSteps(string displayName)
        {
            this.NodeGroupBuilder = new NodeGroupBuilder(Path.Combine(this.GetType().Name, displayName), KnownNetworks.StratisRegTest);
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
                    .WithWallet(this.PremineWallet, this.PremineWalletPassword, this.PremineWalletPassphrase)
                    .Build();
        }

        public void MineGenesisAndPremineBlocks()
        {
            int premineBlockCount = 2;
            
            var addressUsed = TestHelper.MineBlocks(this.nodes[this.PremineNode], this.PremineWallet, this.PremineWalletPassword, this.PremineWalletAccount, (uint)premineBlockCount).AddressUsed;

            // Since the pre-mine will not be immediately spendable, the transactions have to be counted directly from the address.
            addressUsed.Transactions.Count().Should().Be(premineBlockCount);

            IConsensus consensus = this.nodes[this.PremineNode].FullNode.Network.Consensus;

            addressUsed.Transactions.Sum(s => s.Amount).Should().Be(consensus.PremineReward + consensus.ProofOfWorkReward);
        }

        public void MineCoinsToMaturity()
        {
            this.nodes[this.PremineNode].GenerateStratisWithMiner(Convert.ToInt32(this.nodes[this.PremineNode].FullNode.Network.Consensus.CoinbaseMaturity));
            TestHelper.WaitForNodeToSync(this.nodes[this.PremineNode]);
        }

        public void PremineNodeMinesTenBlocksMoreEnsuringTheyCanBeStaked()
        {
            this.nodes[this.PremineNode].GenerateStratisWithMiner(10);
        }

        public void PremineNodeStartsStaking()
        {
            // Get set of transaction IDs present in wallet before staking is started.
            this.transactionsBeforeStaking.Clear();
            foreach (TransactionData transactionData in this.nodes[this.PremineNode].FullNode.WalletManager().Wallets
                .First()
                .GetAllTransactionsByCoinType((CoinType)this.nodes[this.PremineNode].FullNode.Network.Consensus
                    .CoinType))
            {
                this.transactionsBeforeStaking.Add(transactionData.Id);
            }

            var minter = this.nodes[this.PremineNode].FullNode.NodeService<IPosMinting>();
            minter.Stake(new WalletSecret() { WalletName = PremineWallet, WalletPassword = PremineWalletPassword });
        }

        public void PremineNodeWalletHasEarnedCoinsThroughStaking()
        {
            // If new transactions are appearing in the wallet, staking has been successful. Due to coin maturity settings the
            // spendable balance of the wallet actually drops after staking, so the wallet balance should not be used to
            // determine whether staking occurred.
            TestHelper.WaitLoop(() =>
            {
                foreach (TransactionData transactionData in this.nodes[this.PremineNode].FullNode.WalletManager().Wallets
                    .First()
                    .GetAllTransactionsByCoinType((CoinType)this.nodes[this.PremineNode].FullNode.Network.Consensus
                        .CoinType))
                {
                    if (!this.transactionsBeforeStaking.Contains(transactionData.Id) && (transactionData.IsCoinStake ?? false))
                    {
                        return true;
                    }
                }

                return false;
            });
        }
    }
}