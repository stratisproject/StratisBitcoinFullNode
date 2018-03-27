using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;
// ReSharper disable ArrangeThisQualifier

namespace Stratis.Bitcoin.IntegrationTests.Mempool
{
    public partial class MempoolRelaySpecification : BddSpecification
    {
        [Fact]
        public void create_transaction_and_broadcast_to_whitelisted_nodes_SHOULD_get_propagated_to_third_peer()
        {
            this.Given(nodeA_nodeB_and_nodeC);
            this.And(nodeA_connects_to_nodeB);
            this.And(nodeB_connects_to_nodeC);
            this.When(nodeA_creates_a_transaction_and_propagates_to_nodeB);
            this.Then(the_transaction_is_propagated_to_nodeC);
        }

        [Fact]
        public void create_transaction_and_broadcast_to_NOT_whitelisted_nodes_SHOULD_get_propagated_to_third_peer()
        {
            this.Given(nodeA_nodeB_and_nodeC);
            this.And(nodeA_connects_to_nodeB);
            this.And(nodeB_connects_to_nodeC);
            this.And(nodeA_nodeB_and_nodeC_are_not_whitelisted);
            this.When(nodeA_creates_a_transaction_and_propagates_to_nodeB);
            this.Then(the_transaction_is_propagated_to_nodeC);
        }
    }
}