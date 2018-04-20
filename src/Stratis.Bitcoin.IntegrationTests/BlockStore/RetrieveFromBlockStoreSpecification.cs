using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;

// Disable warnings about "this" qualifier to make the Specification more readable
// ReSharper disable ArrangeThisQualifier

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class RetrieveFromBlockStoreSpecification : BddSpecification
    {
        [Fact]
        public void A_block_can_be_retrieved_by_its_identifier_and_a_fake_block_cannot_be_retrieved()
        {
            Given(a_pow_node_running);
            And(a_miner_validating_blocks);
            And(some_real_blocks_with_a_uint256_identifier);
            And(a_made_up_block_id);
            And(the_node_is_synced);

            When(trying_to_retrieve_the_blocks_from_the_blockstore);

            Then(real_blocks_should_be_retrieved);
            Then(made_up_blocks_should_not_be_retrieved);
        }

        [Fact]
        public void A_transaction_can_be_retrieved_by_its_identifier_and_a_fake_transaction_cannot_be_retrieved()
        {
            Given(a_pow_node_running);
            And(a_pow_node_to_transact_with);
            And(a_miner_validating_blocks);
            And(some_blocks_creating_reward);
            And(the_nodes_are_synced);
            And(a_real_transaction);
            And(a_made_up_transaction_id);
            And(the_block_with_the_transaction_is_mined);

            When(trying_to_retrieve_the_transactions_by_Id_from_the_blockstore);
            And(trying_to_retrieve_the_block_containing_the_transactions_from_the_blockstore);

            Then(the_real_transaction_should_be_retrieved);
            And(the_block_with_the_real_transaction_should_be_retrieved);
            And(the_made_up_transaction_should_not_be_retrieved);
            And(the_block_with_the_made_up_transaction_should_not_be_retrieved);
        }

    }
}
