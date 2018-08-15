using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderRuleTest
    {
        private readonly Network network;

        public BlockHeaderRuleTest()
        {
            this.network = KnownNetworks.RegTest;
        }

        [Fact]
        public async Task BlockReceived_IsNextBlock_ValidationSucessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            var blockHeaderRule = testContext.CreateRule<SetActivationDeploymentsPartialValidationRule>();

            var context = new PowRuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = KnownNetworks.RegTest.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.BlockToValidate.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
            context.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(context.ValidationContext.BlockToValidate.Header, context.ValidationContext.BlockToValidate.Header.GetHash(), 0);

            await blockHeaderRule.RunAsync(context);

            Assert.NotNull(context.ValidationContext.ChainedHeaderToValidate);
            Assert.NotNull(context.Flags);
        }
    }
}
