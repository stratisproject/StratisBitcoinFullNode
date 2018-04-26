using System;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class WalletNotNotifiedWhenSendingAnInvalidTransaction
    {
        [Fact]
        public void Sending_Recently_Stakes_Coins_Will_Show_In_The_Wallets_Transaction_History()
        {
            Given(two_nodes_which_includes_a_proof_of_stake_node_with_a_million_coins);
            And(a_wallet_with_coins);
            When(a_wallet_sends_all_coins_and_fails);
            Then(the_wallet_history_shows_the_transaction_as_failed_not_pending);
        }

        [Fact]
        public void Sending_Coins_With_A_High_Fee_Will_Faill_And_Show_In_The_Wallets_Transaction_History()
        {
            Given(two_nodes_which_includes_a_proof_of_stake_node_with_a_million_coins);
            And(a_wallet_with_coins);
            When(a_wallet_sends_coins_with_a_high_fee_type);
            Then(the_wallet_history_shows_the_transaction_as_failed_not_pending);
        }
    }
}
