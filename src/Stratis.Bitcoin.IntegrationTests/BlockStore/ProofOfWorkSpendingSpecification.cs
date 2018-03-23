using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ProofOfWorkSpendingSpec : BddSpecification
    {
        [Fact]
        public void Attempt_to_spend_coin_earned_through_proof_of_work_before_coin_maturity_will_fail()
        {
            Given(a_stratis_bitcoin_node_and_wallet);
            And(proof_of_work_blocks_mined_to_just_before_maturity);
            When(i_try_to_spend_the_coins);
            Then(the_transaction_should_be_rejected_from_the_mempool);
        }

        [Fact]
        public void Attempt_to_spend_coin_earned_through_proof_of_work_after_maturity_will_succeed()
        {
            Given(a_stratis_bitcoin_node_and_wallet);
            And(proof_of_work_blocks_mined_past_maturity);
            When(i_try_to_spend_the_coins);
            Then(the_transaction_should_be_accepted_by_the_mempool);
        }
    }
}
