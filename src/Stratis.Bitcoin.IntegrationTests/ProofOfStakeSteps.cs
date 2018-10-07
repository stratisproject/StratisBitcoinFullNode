using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ProofOfStakeSteps
    {
        public readonly NodeBuilder nodeBuilder;
        public CoreNode PremineNodeWithCoins;

        public readonly string PremineNode = "PremineNode";
        public readonly string PremineWallet = "mywallet";
        public readonly string PremineWalletAccount = "account 0";
        public readonly string PremineWalletPassword = "password";

        private readonly HashSet<uint256> transactionsBeforeStaking = new HashSet<uint256>();

        public ProofOfStakeSteps(string displayName)
        {

            this.nodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, displayName));
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

        public void PremineNodeWithWallet()
        {
            this.PremineNodeWithCoins = this.nodeBuilder.CreateStratisPosNode(KnownNetworks.StratisRegTest).NotInIBD().WithWallet();
            this.PremineNodeWithCoins.Start();
        }

        public void MineGenesisAndPremineBlocks()
        {
            int premineBlockCount = 2;

            var addressUsed = TestHelper.MineBlocks(this.PremineNodeWithCoins, premineBlockCount).AddressUsed;

            // Since the pre-mine will not be immediately spendable, the transactions have to be counted directly from the address.
            addressUsed.Transactions.Count().Should().Be(premineBlockCount);

            IConsensus consensus = this.PremineNodeWithCoins.FullNode.Network.Consensus;

            addressUsed.Transactions.Sum(s => s.Amount).Should().Be(consensus.PremineReward + consensus.ProofOfWorkReward);
        }

        public void MineCoinsToMaturity()
        {
            TestHelper.MineBlocks(this.PremineNodeWithCoins, (int)this.PremineNodeWithCoins.FullNode.Network.Consensus.CoinbaseMaturity);
        }

        public void PremineNodeMinesTenBlocksMoreEnsuringTheyCanBeStaked()
        {
            TestHelper.MineBlocks(this.PremineNodeWithCoins, 10);
        }

        public void PremineNodeStartsStaking()
        {
            // Get set of transaction IDs present in wallet before staking is started.
            this.transactionsBeforeStaking.Clear();
            foreach (TransactionData transactionData in this.PremineNodeWithCoins.FullNode.WalletManager().Wallets
                .First()
                .GetAllTransactionsByCoinType((CoinType)this.PremineNodeWithCoins.FullNode.Network.Consensus
                    .CoinType))
            {
                this.transactionsBeforeStaking.Add(transactionData.Id);
            }

            var minter = this.PremineNodeWithCoins.FullNode.NodeService<IPosMinting>();
            minter.Stake(new WalletSecret() { WalletName = PremineWallet, WalletPassword = PremineWalletPassword });
        }

        public void PremineNodeWalletHasEarnedCoinsThroughStaking()
        {
            // If new transactions are appearing in the wallet, staking has been successful. Due to coin maturity settings the
            // spendable balance of the wallet actually drops after staking, so the wallet balance should not be used to
            // determine whether staking occurred.
            TestHelper.WaitLoop(() =>
            {
                foreach (TransactionData transactionData in this.PremineNodeWithCoins.FullNode.WalletManager().Wallets
                    .First()
                    .GetAllTransactionsByCoinType((CoinType)this.PremineNodeWithCoins.FullNode.Network.Consensus
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