using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingTransactionWithDoubleSpend
    {
        [Fact]
        public void sending_transaction_with_double_spend()
        {
            Given(wallets_with_coins);
            And(coins_first_sent_to_receiving_wallet);
            And(trx_is_consumed_from_mempool_and_mined_into_a_block);
            Then(receiving_node_attempts_to_double_spend_mempool_doesnotaccept);
        }
    }
}
