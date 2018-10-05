using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public partial class ProofOfStakeMintCoinsSpecification : BddSpecification
    {
        [Fact]
        public void Staking_wallet_can_mint_new_coins()
        {
            Given(a_proof_of_stake_node_with_wallet);
            And(it_mines_genesis_and_premine_blocks);
            And(mine_coins_to_maturity);
            And(pos_node_mines_ten_blocks_more_ensuring_they_can_be_staked);
            When(pos_node_starts_staking);
            Then(pos_node_wallet_has_earned_coins_through_staking);
        }

        [Fact]
        public void MiningAndPropagatingPOS_MineBlockCheckPeerHasNewBlock()
        {
            Given(a_proof_of_stake_node_with_wallet);
            And(it_mines_genesis_and_premine_blocks);
            And(mine_coins_to_maturity);
            And(pos_node_mines_ten_blocks_more_ensuring_they_can_be_staked);
            Then(pos_node_adds_peernode_and_propagate_blocks);
        }

        [Fact]
        public void MiningAndPropagatingPOS_MineBlockStakeAtInsufficientHeightError()
        {
            Given(a_proof_of_stake_node_with_wallet);
            And(it_mines_genesis_and_premine_blocks);
            And(mine_coins_to_maturity);
            And(last_pow_block_height_is_set_to_1);
            And(pos_node_mines_ten_blocks_more_ensuring_they_can_be_staked);
            Then(pow_too_high_consensus_error_thrown);
        }
    }
}