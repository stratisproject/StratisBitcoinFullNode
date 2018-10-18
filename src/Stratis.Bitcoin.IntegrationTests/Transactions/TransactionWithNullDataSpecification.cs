using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit;

// Disable warnings about "this" qualifier to make the Specification more readable
// ReSharper disable ArrangeThisQualifier

namespace Stratis.Bitcoin.IntegrationTests.Transactions
{
    public partial class TransactionWithNullDataSpecification : BddSpecification
    {
        [Fact]
        public void A_nulldata_transaction_is_sent_to_the_network()
        {
            Given(two_proof_of_work_nodes);
            And(a_sending_and_a_receiving_wallet);
            And(some_funds_in_the_sending_wallet);
            And(no_fund_in_the_receiving_wallet);
            And(the_wallets_are_in_sync);
            And(a_nulldata_transaction);
            When(the_transaction_is_broadcasted);
            And(the_block_is_mined);
            Then(the_transaction_should_get_confirmed);
            And(the_transaction_should_appear_in_the_blockchain);
        }
    }
}
