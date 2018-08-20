using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingStakedCoinsBeforeMaturity
    {
        [Fact]
        public void Sending_Coins_Before_Maturity_Fails()
        {
            Given(two_pos_nodes_with_one_node_having_a_wallet_with_premined_coins);
            When(a_wallet_sends_coins_before_maturity);
            Then(the_wallet_history_does_not_include_the_transaction);
            And(the_transaction_was_not_received);
        }
    }
}
