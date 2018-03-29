using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class WalletTestsManyTxnInputsAndOutputsSpecification 
    {
        [Fact]
        public void Wallet_can_send_funds_with_many_txn_outputs()
        {
            Given(a_sender_and_receiver_and_their_wallets);
            And(a_block_is_mined);
            When(funds_are_sent_to_receiver_via_fifty_txn_outputs);
            Then(the_funds_are_received);
        }

        [Fact]
        public void Wallet_can_receive_funds_using_many_txn_inputs()
        {
            Given(sender_sends_funds_with_fifty_txn_outputs);
            When(the_receiver_creates_a_txn_using_fifty_txn_inputs_and_sends_back);
            Then(the_sender_receives_the_funds);
        }
    }
}
