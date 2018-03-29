using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReOrgRegularlySpecification : BddSpecification
    {
        [Fact]
        public void A_selfishly_mining_node_broadcasts_longer_chain_causing_reorg()
        {
            Given(four_nodes);
            And(each_mine_a_block);
            And(mining_continues_to_maturity_to_allow_spend);
            And(selfish_miner_disconnects_and_mines_10_blocks);
            And(second_node_creates_a_transaction_and_broadcasts);
            And(third_node_mines_this_block);
            And(fouth_node_confirms_it_ensures_tx_present);
            When(selfish_node_reconnects_and_broadcasts);
            Then(second_third_and_fourth_node_reorg_to_longest_chain);
            And(transaction_from_shorter_chain_is_missing);
            And(transaction_is_NOT_YET_returned_to_the_mem_pool);
        }
    }
}