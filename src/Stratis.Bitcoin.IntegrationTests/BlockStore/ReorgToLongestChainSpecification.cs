using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReorgToLongestChainSpecification : BddSpecification
    {
        [Fact]
        public void A_selfishly_mining_node_broadcasts_longer_chain_causing_reorg()
        {
            Given(four_nodes);
            And(each_mine_a_block);
            And(mining_continues_to_maturity_to_allow_spend);
            And(selfish_miner_disconnects_and_mines_10_blocks);
            And(bob_creates_a_transaction_and_broadcasts);
            And(charlie_mines_this_block);
            And(dave_confirms_it_ensures_tx_present);
            When(selfish_simon_reconnects_and_broadcasts_his_longer_chain);
            Then(bob_charlie_and_dave_reorg_to_selfish_simons_longest_chain);
            And(bobs_transaction_from_shorter_chain_is_now_missing);
            And(bobs_transaction_is_not_returned_to_the_mem_pool); // TODO: Inverse this check and implement it in production code
        }
    }
}