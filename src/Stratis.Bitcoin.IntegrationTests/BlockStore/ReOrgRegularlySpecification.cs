using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReOrgRegularlySpecification : BddSpecification
    {
        [Fact]
        public void A_selfishly_mining_node_broadcasts_every_20_blocks()
        {
            Given(four_nodes);
            And(each_mine_10_blocks);
            And(first_node_disconnects_and_selfishly_mines_10_blocks);
            And(second_node_creates_a_transaction_and_broadcasts);
            And(third_node_mines_this_block);
            And(fouth_node_confirms_it);
            When(first_node_reconnects_and_broadcasts);
            Then(second_third_and_fourth_node_reorg_to_longest_chain);
            And(transaction_from_shorter_chain_is_missing);
            And(transaction_is_returned_to_the_mem_pool);
        }
    }
}