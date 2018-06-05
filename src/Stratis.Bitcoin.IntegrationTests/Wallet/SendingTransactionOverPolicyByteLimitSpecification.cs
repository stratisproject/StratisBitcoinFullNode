using Remotion.Linq.Clauses;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingTransactionOverPolicyByteLimit
    {
        [Fact]
        public void sending_transaction_near_policy_byte_limit()
        {
            Given(two_connected_nodes);
            And(node1_builds_undersize_transaction_to_send_to_node2);
            And(serialized_size_of_transaction_is_within_1KB_of_upper_limit);
            When(sending_the_transaction);
            Then(node1_succeeds_sending_tx_to_node2);
        }

        [Fact]
        public void sending_transaction_over_policy_byte_limit()
        {
            Given(two_connected_nodes);
            And(node1_builds_oversize_tx_to_send_to_node2);
            Then(node1_fails_with_oversize_transaction_wallet_error);
        }
    }
}
