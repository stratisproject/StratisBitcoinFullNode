using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    public class BlockHeaderPowContextualRuleTest
    {
        public BlockHeaderPowContextualRuleTest()
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;
        }

        [Fact]
        public async Task BlockReceived_CheckHeaderBits_ValidationFailAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            BlockHeaderPowContextualRule rule = testContext.CreateRule<BlockHeaderPowContextualRule>();

            var context = new RuleContext(new BlockValidationContext (), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block(new BlockHeader { HashPrevBlock = testContext.Chain.Tip.HashBlock, Bits = 100 });
            context.BlockValidationContext.ChainedBlock = new ChainedBlock(context.BlockValidationContext.Block.Header, context.BlockValidationContext.Block.Header.GetHash(NetworkOptions.TemporaryOptions), context.ConsensusTip);

            // increment the bits.
            context.SetBestBlock(DateTimeProvider.Default.GetTimeOffset());
            context.NextWorkRequired = context.BlockValidationContext.ChainedBlock.GetNextWorkRequired(Network.RegTest.Consensus);

            try
            {
                await rule.RunAsync(context);
            }
            catch (ConsensusErrorException cee)
            {
                Assert.Equal(ConsensusErrors.BadDiffBits, cee.ConsensusError);
            }
        }
    }
}
