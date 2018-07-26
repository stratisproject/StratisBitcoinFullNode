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
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Networks.RegTest);
            var blockHeaderRule = testContext.CreateRule<SetActivationDeploymentsRule>();

            var context = new PowRuleContext(new ValidationContext(), Networks.RegTest.Consensus, testContext.Chain.Tip, testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.Block = Networks.RegTest.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.Block.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
            context.ValidationContext.ChainedHeader = new ChainedHeader(context.ValidationContext.Block.Header, context.ValidationContext.Block.Header.GetHash(), 0);

            await blockHeaderRule.RunAsync(context);

            Assert.NotNull(context.ValidationContext.ChainedHeader);
            Assert.NotNull(context.ConsensusTip);
            Assert.NotNull(context.Flags);
        }
    }
}
