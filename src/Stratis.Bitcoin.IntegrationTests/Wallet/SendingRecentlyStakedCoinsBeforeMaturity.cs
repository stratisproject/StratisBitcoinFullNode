using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingRecentlyStakedCoinsBeforeMaturity
    {
        [Fact]
        public void Sending_Staked_Coins_Before_Maturity_Is_Not_Reflected_In_The_Wallets_Transaction_History()
        {
            Given(two_nodes_which_includes_a_proof_of_stake_node_with_a_million_coins);
            When(a_wallet_sends_staked_coins_before_maturity);
            Then(the_wallet_history_shows_the_transaction_as_sent);
        }
    }
}
