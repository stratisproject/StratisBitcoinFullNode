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
            When(two_transactions_attempt_to_spend_same_unspent_outputs);
            Then(mempool_rejects_doublespending_transaction);
        }
    }
}
