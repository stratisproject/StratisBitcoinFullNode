using System;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class AssumeValidRuleTest : ConsensusRuleUnitTestBase
    {
        private AssumeValidRule rule;

        public AssumeValidRuleTest() : base(Network.TestNet)
        {
            this.network.Consensus.Options = new PowConsensusOptions();
            AddBlocksToChain(this.concurrentChain, 5);
            this.rule = this.CreateRule();
        }

        [Fact]
        public void Initialize_CheckpointsRuleInConsensusRules_DoesNotThrowException()
        {
            (this.rule.Parent as TestConsensusRules).RegisterRule<CheckpointsRule>();

            this.rule.Initialize();
        }

        [Fact]
        public void Initialize_CheckpointsRuleNotInConsensusRules_ThrowException()
        {
            Assert.Throws<Exception>(() =>
            {
                this.rule.Initialize();
            });
        }

        [Fact]
        public void RunAsync_SkipValidation_ReturnsCompletedTask()
        {
            this.ruleContext.SkipValidation = true;
            Assert.True(this.rule.RunAsync(this.ruleContext).GetAwaiter().IsCompleted);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidNotSetOnConsensus_ReturnsCompletedTask()
        {
            this.consensusSettings.BlockAssumedValid = null;
            this.ruleContext.SkipValidation = false;

            var awaiter = this.rule.RunAsync(this.ruleContext).GetAwaiter();

            Assert.True(awaiter.IsCompleted);
            Assert.False(this.ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockNotOnChain_DoesNotSetSkipValidation()
        {
            this.consensusSettings.BlockAssumedValid = new uint256(25);
            this.ruleContext.SkipValidation = false;

            var awaiter = this.rule.RunAsync(this.ruleContext).GetAwaiter();

            Assert.True(awaiter.IsCompleted);
            Assert.False(this.ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockLowerThanAssumedValidHeight_SetSkipValidation()
        {            
            this.consensusSettings.BlockAssumedValid = this.concurrentChain.GetBlock(4).HashBlock;
            this.ruleContext.SkipValidation = false;
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(3);

            var awaiter = this.rule.RunAsync(this.ruleContext).GetAwaiter();

            Assert.True(awaiter.IsCompleted);
            Assert.True(this.ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockEqualToThanAssumedValidHeight_SetSkipValidation()
        {            
            this.consensusSettings.BlockAssumedValid = this.concurrentChain.GetBlock(4).HashBlock;
            this.ruleContext.SkipValidation = false;
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(4);

            var awaiter = this.rule.RunAsync(this.ruleContext).GetAwaiter();

            Assert.True(awaiter.IsCompleted);
            Assert.True(this.ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockHigherThanAssumedValidHeight_DoesNotSetSkipValidation()
        {            
            this.consensusSettings.BlockAssumedValid = this.concurrentChain.GetBlock(3).HashBlock;
            this.ruleContext.SkipValidation = false;
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(4);

            var awaiter = this.rule.RunAsync(this.ruleContext).GetAwaiter();

            Assert.True(awaiter.IsCompleted);
            Assert.False(this.ruleContext.SkipValidation);
        }

        private AssumeValidRule CreateRule()
        {
            return new AssumeValidRule()
            {
                Logger = this.logger.Object,
                Parent = new TestConsensusRules(this.network, this.loggerFactory.Object, this.dateTimeProvider.Object, this.concurrentChain, this.nodeDeployments, this.consensusSettings, this.checkpoints.Object)
            };
        }
    }
}