using System.Threading.Tasks;
using NBitcoin;
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

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = Network.RegTest.Consensus.ConsensusFactory.CreateBlock();
            context.BlockValidationContext.Block.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;

            await blockHeaderRule.RunAsync(context);

            Assert.NotNull(context.BlockValidationContext.ChainedHeader);
            Assert.NotNull(context.BestBlock);
            Assert.NotNull(context.Flags);
        }

        [Fact]
        public async Task BlockReceived_NotNextBlock_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            var blockHeaderRule = testContext.CreateRule<BlockHeaderRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = Network.RegTest.Consensus.ConsensusFactory.CreateBlock();
            context.BlockValidationContext.Block.Header.HashPrevBlock = uint256.Zero;
            ConsensusErrorException error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await blockHeaderRule.RunAsync(context));

            Assert.Equal(ConsensusErrors.InvalidPrevTip, error.ConsensusError);
        }
    }
}
