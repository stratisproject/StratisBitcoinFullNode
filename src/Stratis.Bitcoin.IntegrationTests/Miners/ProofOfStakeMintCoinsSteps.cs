using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public partial class ProofOfStakeMintCoinsSpecification
    {
        private ProofOfStakeSteps proofOfStakeSteps;

        public ProofOfStakeMintCoinsSpecification(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
            this.proofOfStakeSteps = new ProofOfStakeSteps(this.CurrentTest.DisplayName);
        }

        protected override void BeforeTest() { }

        protected override void AfterTest() { }

        private void a_proof_of_work_node_with_wallet()
        {
            this.proofOfStakeSteps.ProofOfWorkNodeWithWallet();
        }

        private void it_mines_genesis_and_premine_blocks()
        {
            this.proofOfStakeSteps.MineGenesisAndPremineBlocks();
        }

        private void mine_coins_to_maturity()
        {
            this.proofOfStakeSteps.MineCoinsToMaturity();
        }

        private void a_proof_of_stake_node_with_wallet()
        {
            this.proofOfStakeSteps.ProofOfStakeNodeWithWallet();
        }

        private void it_syncs_with_proof_work_node()
        {
            this.proofOfStakeSteps.SyncWithProofWorkNode();
        }

        private void sends_a_million_coins_from_pow_wallet_to_pos_wallet()
        {
            this.proofOfStakeSteps.SendOneMillionCoinsFromPowWalletToPosWallet();
        }

        private void pow_wallet_broadcasts_tx_of_million_coins_and_pos_wallet_receives()
        {
            this.proofOfStakeSteps.PowWalletBroadcastsTransactionOfOneMillionCoinsAndPosWalletReceives();
        }

        private void pos_node_mines_ten_blocks_more_ensuring_they_can_be_staked()
        {
            this.proofOfStakeSteps.PosNodeMinesTenBlocksMoreEnsuringTheyCanBeStaked();
        }

        private void pos_node_starts_staking()
        {
            this.proofOfStakeSteps.PosNodeStartsStaking();
        }

        private void pos_node_wallet_has_earned_coins_through_staking()
        {
            this.proofOfStakeSteps.PosNodeWalletHasEarnedCoinsThroughStaking();
        }
    }
}