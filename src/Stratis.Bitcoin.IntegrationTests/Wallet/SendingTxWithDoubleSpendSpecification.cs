using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingTxWithDoubleSpend
    {
        [Fact]
        public void sending_tx_with_double_spend()
        {
            Given(wallets_with_coins);
            When(coins_first_sent_to_receiving_wallet);
            Then(attempt_made_to_spend_same_coins);
            Then(mempool_rejects_doublespent_transaction);
        }
    }
}
