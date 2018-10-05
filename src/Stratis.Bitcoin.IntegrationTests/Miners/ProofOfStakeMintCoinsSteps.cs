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

        private void a_proof_of_stake_node_with_wallet()
        {
            this.proofOfStakeSteps.PremineNodeWithWallet();
        }

        private void it_mines_genesis_and_premine_blocks()
        {
            this.proofOfStakeSteps.MineGenesisAndPremineBlocks();
        }

        private void mine_coins_to_maturity()
        {
            this.proofOfStakeSteps.MineCoinsToMaturity();
        }

        private void pos_node_mines_ten_blocks_more_ensuring_they_can_be_staked()
        {
            this.proofOfStakeSteps.PremineNodeMinesTenBlocksMoreEnsuringTheyCanBeStaked();
        }

        private void pos_node_starts_staking()
        {
            this.proofOfStakeSteps.PremineNodeStartsStaking();
        }

        private void pos_node_wallet_has_earned_coins_through_staking()
        {
            this.proofOfStakeSteps.PremineNodeWalletHasEarnedCoinsThroughStaking();
        }

        private void pos_node_adds_peernode_and_propagate_blocks()
        {
            this.proofOfStakeSteps.PremineNodeAddsPeerAndBlocksPropagate();
        }

        private void last_pow_block_height_is_set_to_1()
        {
            this.proofOfStakeSteps.SetLastPowBlockHeightToOne();
        }

        private void pow_too_high_consensus_error_thrown()
        {
            this.proofOfStakeSteps.PowTooHighConsensusErrorThrown();
        }
    }
}