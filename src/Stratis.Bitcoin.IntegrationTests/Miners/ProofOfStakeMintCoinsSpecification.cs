﻿using Stratis.Bitcoin.IntegrationTests.TestFramework;
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
            And(create_tx_to_send_million_coins_from_pow_wallet_to_pos_node_wallet);
            And(pow_wallet_broadcasts_tx_of_million_coins_and_pos_wallet_receives);
            And(pos_node_mines_a_further_ten_blocks);
            When(pos_node_starts_staking);
            Then(pos_node_wallet_has_earned_coins_through_staking);
        }
    }
}