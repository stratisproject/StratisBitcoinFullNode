using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class WalletTestsManyTxnInputsAndOutputsSpecification 
    {
        [Fact]
        public void Wallet_can_send_one_transaction_with_many_outputs()
        {
            Given(a_stratis_sender_and_receiver_node_and_their_wallets);
            And(a_block_is_minded);
            Then(many_transaction_inputs_go_to_the_sender);
        }

        [Fact]
        public void Wallet_can_send_and_receive_many_transaction_inputs_and_ouputs()
        {
            Given(a_stratis_sender_and_receiver_node_and_their_wallets);
            And(a_block_is_minded);
            When(many_transaction_inputs_go_to_the_sender);
            When(many_transaction_outputs_go_back_to_the_receiver);
            Then(the_receiver_has_many_inputs);
        }
    }
}
