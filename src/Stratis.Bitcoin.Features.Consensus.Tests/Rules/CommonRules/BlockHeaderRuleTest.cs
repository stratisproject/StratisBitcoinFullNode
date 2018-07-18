using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderRuleTest
    {
        [Fact]
        public async Task BlockReceived_IsNextBlock_ValidationSucessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            var blockHeaderRule = testContext.CreateRule<BlockHeaderRule>();

            var context = new PowRuleContext(new ValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.ValidationContext.Block = Network.RegTest.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.Block.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;

            await blockHeaderRule.RunAsync(context);

            Assert.NotNull(context.ValidationContext.ChainedHeader);
            Assert.NotNull(context.ConsensusTip);
            Assert.NotNull(context.Flags);
        }

        [Fact]
        public async Task BlockReceived_NotNextBlock_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            var blockHeaderRule = testContext.CreateRule<BlockHeaderRule>();

            var context = new PowRuleContext(new ValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.ValidationContext.Block = Network.RegTest.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.Block.Header.HashPrevBlock = uint256.Zero;
            ConsensusErrorException error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await blockHeaderRule.RunAsync(context));

            Assert.Equal(ConsensusErrors.InvalidPrevTip, error.ConsensusError);
        }
    }
}
