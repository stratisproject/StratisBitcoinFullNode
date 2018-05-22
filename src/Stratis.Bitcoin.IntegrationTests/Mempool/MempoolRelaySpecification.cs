using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Mempool
{
    public partial class MempoolRelaySpecification : BddSpecification
    {
        [Fact]
        public void create_transaction_and_broadcast_to_whitelisted_nodes_SHOULD_get_propagated_to_third_peer()
        {
            Given(nodeA_nodeB_and_nodeC);
            And(nodeA_mines_coins_that_are_spendable);
            And(nodeA_connects_to_nodeB);
            And(nodeB_connects_to_nodeC);
            When(nodeA_creates_a_transaction_and_propagates_to_nodeB);
            Then(the_transaction_is_propagated_to_nodeC);
        }

        [Fact]
        public void create_transaction_and_broadcast_to_NON_whitelisted_nodes_SHOULD_get_propagated_to_third_peer()
        {
            Given(nodeA_nodeB_and_nodeC);
            And(nodeA_mines_coins_that_are_spendable);
            And(nodeA_connects_to_nodeB);
            And(nodeB_connects_to_nodeC);
            And(nodeA_nodeB_and_nodeC_are_NON_whitelisted);
            When(nodeA_creates_a_transaction_and_propagates_to_nodeB);
            Then(the_transaction_is_propagated_to_nodeC);
        }
    }
}