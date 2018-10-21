using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReorgToLongestChainSpecification : BddSpecification
    {
        [Fact]
        public void A_cut_off_miner_advanced_ahead_of_network_causes_reorg_on_reconnect()
        {
            Given(four_miners);
            And(each_mine_a_block);
            And(mining_continues_to_maturity_to_allow_spend);
            And(jing_loses_connection_to_others_but_carries_on_mining);
            And(bob_creates_a_transaction_and_broadcasts);
            And(charlie_waits_for_the_trx_and_mines_this_block);
            And(dave_confirms_transaction_is_present);
            And(meanwhile_jings_chain_advanced_ahead_of_the_others);
            When(jings_connection_comes_back);
            Then(bob_charlie_and_dave_reorg_to_jings_longest_chain);
            And(bobs_transaction_from_shorter_chain_is_now_missing);
            But(bobs_transaction_is_now_in_the_mem_pool);
        }
    }
}