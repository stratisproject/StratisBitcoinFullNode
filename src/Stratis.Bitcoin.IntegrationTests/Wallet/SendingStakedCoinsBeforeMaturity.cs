using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingStakedCoinsBeforeMaturity
    {
        [Fact]
        public void Sending_Coins_Before_Maturity_Are_Not_Included_In_The_Wallets_Transaction_History()
        {
            Given(two_nodes_which_includes_a_proof_of_stake_wallet_with_over_a_million_coins);
            When(a_wallet_sends_coins_before_maturity);
            Then(the_wallet_history_does_not_include_the_transaction);
            And(the_transaction_was_not_received);
        }
    }
}
