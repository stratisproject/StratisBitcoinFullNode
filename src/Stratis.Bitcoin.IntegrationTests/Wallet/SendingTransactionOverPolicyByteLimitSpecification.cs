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
            And(serialized_size_of_transaction_is_close_to_upper_limit);
            And(node1_wallet_throws_no_exceptions);
            When(sending_the_transaction);
            Then(mempool_of_node2_has_received_transaction);
        }

        [Fact]
        public void sending_transaction_over_policy_byte_limit()
        {
            Given(two_connected_nodes);
            And(node1_builds_oversize_tx_to_send_to_node2);
            Then(node1_fails_with_oversize_transaction_wallet_error);
            Then(mempool_of_receiver_node2_is_empty);
        }
    }
}
