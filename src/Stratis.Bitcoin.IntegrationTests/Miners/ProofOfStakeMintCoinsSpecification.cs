using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public partial class ProofOfStakeMintCoinsSpecification : BddSpecification
    {
        [Fact]
        public void Staking_wallet_can_mint_new_coins()
        {
            Given(a_proof_of_work_node_with_wallet);
            And(it_mines_genesis_and_premine_blocks);
            And(mine_coins_to_maturity);
            And(a_proof_of_stake_node_with_wallet);
            And(it_syncs_with_proof_work_node);
            And(sends_a_million_coins_from_pow_wallet_to_pos_wallet);
            And(pow_wallet_broadcasts_tx_of_million_coins_and_pos_wallet_receives);
            And(pos_node_mines_ten_blocks_more_ensuring_they_can_be_staked);
            When(pos_node_starts_staking);
            Then(pos_node_wallet_has_earned_coins_through_staking);
        }

        [Fact]
        public void Staking_wallet_fails_when_trying_to_spend_rewards_before_maturity()
        {
            Given(a_staking_wallet_minting_coins);
            When(it_creates_a_transaction_to_spend);
            Then(it_is_rejected_because_of_no_spendable_coins);
        }
    }
}