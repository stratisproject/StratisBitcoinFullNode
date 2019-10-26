using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingTransactionWithDoubleSpend
    {
        [Fact]
        public void sending_transaction_with_double_spend_mined_trx()
        {
            Given(wallets_with_coins);
            And(coins_first_sent_to_receiving_wallet);
            And(trx_is_propagated_across_sending_and_receiving_mempools);
            And(trx_is_mined_into_a_block_and_removed_from_mempools);
            Then(receiving_node_attempts_to_double_spend_mempool_doesnotaccept);
        }

        [Fact]
        public void sending_transaction_with_double_spend_in_mempool()
        {
            Given(wallets_with_coins);
            And(coins_first_sent_to_receiving_wallet);
            And(trx_is_propagated_across_sending_and_receiving_mempools);
            Then(receiving_node_attempts_to_double_spend_mempool_doesnotaccept);
            Then(txn_mempool_conflict_error_occurs);
        }
    }
}
