using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderPowContextualRuleTest
    {
        [Fact]
        public async Task CheckHeaderBits_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            BlockHeaderPowContextualRule rule = testContext.CreateRule<BlockHeaderPowContextualRule>();

            RuleContext context = new RuleContext(new BlockValidationContext (), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = TestRulesContextFactory.MineBlock(Network.RegTest, testContext.Chain);
            context.BlockValidationContext.ChainedBlock = new ChainedBlock(context.BlockValidationContext.Block.Header, context.BlockValidationContext.Block.Header.GetHash(), context.ConsensusTip);
            context.SetBestBlock(DateTimeProvider.Default.GetTimeOffset());

            // increment the bits.
            context.NextWorkRequired = context.BlockValidationContext.ChainedBlock.GetNextWorkRequired(Network.RegTest.Consensus);
            context.BlockValidationContext.Block.Header.Bits += 1;

            var error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
            Assert.Equal(ConsensusErrors.BadDiffBits, error.ConsensusError);
        }

        [Fact]
        public async Task ChecBlockPreviousTimestamp_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            BlockHeaderPowContextualRule rule = testContext.CreateRule<BlockHeaderPowContextualRule>();

            RuleContext context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = TestRulesContextFactory.MineBlock(Network.RegTest, testContext.Chain);
            context.BlockValidationContext.ChainedBlock = new ChainedBlock(context.BlockValidationContext.Block.Header, context.BlockValidationContext.Block.Header.GetHash(), context.ConsensusTip);
            context.SetBestBlock(DateTimeProvider.Default.GetTimeOffset());

            // increment the bits.
            context.NextWorkRequired = context.BlockValidationContext.ChainedBlock.GetNextWorkRequired(Network.RegTest.Consensus);
            context.BlockValidationContext.Block.Header.BlockTime = context.BestBlock.Header.BlockTime.AddSeconds(-1);

            var error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
            Assert.Equal(ConsensusErrors.TimeTooOld, error.ConsensusError);
        }

        [Fact]
        public async Task ChecBlockFutureTimestamp_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            BlockHeaderPowContextualRule rule = testContext.CreateRule<BlockHeaderPowContextualRule>();

            RuleContext context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = TestRulesContextFactory.MineBlock(Network.RegTest, testContext.Chain);
            context.BlockValidationContext.ChainedBlock = new ChainedBlock(context.BlockValidationContext.Block.Header, context.BlockValidationContext.Block.Header.GetHash(), context.ConsensusTip);
            context.SetBestBlock(DateTimeProvider.Default.GetTimeOffset());

            // increment the bits.
            context.NextWorkRequired = context.BlockValidationContext.ChainedBlock.GetNextWorkRequired(Network.RegTest.Consensus);
            context.BlockValidationContext.Block.Header.BlockTime = context.Time.AddHours(3);

            var error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
            Assert.Equal(ConsensusErrors.TimeTooNew, error.ConsensusError);
        }
    }
}
