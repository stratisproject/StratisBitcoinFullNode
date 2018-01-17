﻿using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    public class BlockHeaderRuleTest
    {
        public BlockHeaderRuleTest()
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;
        }

        [Fact]
        public async Task BlockReceived_IsNextBlock_ValidationSucessAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            BlockHeaderRule blockHeaderRule = testContext.CreateRule<BlockHeaderRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block(new BlockHeader { HashPrevBlock = testContext.Chain.Tip.HashBlock });
            await blockHeaderRule.RunAsync(context);

            Assert.NotNull(context.BlockValidationContext.ChainedBlock);
            Assert.NotNull(context.BestBlock);
            Assert.NotNull(context.Flags);
        }

        [Fact]
        public async Task BlockReceived_NotNextBlock_ValidationFailAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            BlockHeaderRule blockHeaderRule = testContext.CreateRule<BlockHeaderRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block(new BlockHeader { HashPrevBlock = uint256.Zero });
            var error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await blockHeaderRule.RunAsync(context));

            Assert.Equal(ConsensusErrors.InvalidPrevTip, error.ConsensusError);
        }
    }
}
