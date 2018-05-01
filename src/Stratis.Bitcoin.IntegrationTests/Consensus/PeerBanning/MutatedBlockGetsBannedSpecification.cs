using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;

// Disable warnings about "this" qualifier to make the Specification more readable
// ReSharper disable ArrangeThisQualifier

namespace Stratis.Bitcoin.IntegrationTests.Consensus.PeerBanning
{
    public partial class MutatedBlockGetsBannedSpecification : BddSpecification
    {
        /// <summary>
        /// This test is not ready yet but I don't want to loose the work, it is hanging
        /// when the malicious miner sends its dodgy block
        /// </summary>
        //[Fact]
        public void MutatedBlockGetsBannedTest()
        {
            Given(three_nodes);
            And(some_coins_to_spend);

            When(a_miner_creates_a_mutated_block_and_broadcasts_it);
            And(another_miner_tries_to_validate_it);

            Then(the_malicious_miner_should_get_banned);
            And(the_block_with_mutated_hash_should_be_rejected);
            And(the_hash_of_the_rejected_block_should_not_be_banned);
        }
    }
}