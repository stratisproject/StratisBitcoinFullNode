using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class WalletTestsManyTxnInputsAndOutputsSpecification 
    {
        [Fact]
        public void Wallet_can_send_one_transaction_with_many_outputs()
        {
            Given(a_stratis_sender_and_receiver_node_and_their_wallets);
            And(a_block_is_mined);
            When(a_transaction_is_sent_to_receiver_via_fifty_outputs);
            Then(the_transaction_is_recevied);
        }

        [Fact]
        public void Wallet_can_send_and_receive_many_transaction_inputs_and_ouputs()
        {
            Given(a_stratis_sender_and_receiver_node_and_their_wallets);
            And(a_block_is_mined);
            When(a_transaction_is_sent_to_receiver_via_fifty_outputs);
            And(the_transaction_is_recevied);
            Then(the_recevier_sends_back_a_transaction_using_all_the_outputs);
        }
    }
}
