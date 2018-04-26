using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class AssumeValidRuleTest : TestConsensusRulesUnitTestBase
    {
        public AssumeValidRuleTest()
        {
        }

        [Fact]
        public void Initialize_CheckpointsRuleInConsensusRules_DoesNotThrowException()
        {
            this.consensusRules.RegisterRule<CheckpointsRule>();

            this.consensusRules.RegisterRule<AssumeValidRule>();
        }

        [Fact]
        public void Initialize_CheckpointsRuleNotInConsensusRules_ThrowException()
        {
            Assert.Throws<Exception>(() =>
            {
                this.consensusRules.RegisterRule<AssumeValidRule>();
            });
        }

        [Fact]
        public void RunAsync_SkipValidation_ReturnsCompletedTask()
        {
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();

            this.ruleContext.SkipValidation = true;
            Assert.True(rule.RunAsync(this.ruleContext).GetAwaiter().IsCompleted);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidNotSetOnConsensus_ReturnsCompletedTask()
        {
            this.consensusRules.ConsensusSettings.BlockAssumedValid = null;
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            this.ruleContext.SkipValidation = false;

            Assert.True(rule.RunAsync(this.ruleContext).GetAwaiter().IsCompleted);
            Assert.False(this.ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockNotOnChain_DoesNotSetSkipValidation()
        {
            this.consensusRules.ConsensusSettings.BlockAssumedValid = new NBitcoin.uint256(25);
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            this.ruleContext.SkipValidation = false;

            Assert.True(rule.RunAsync(this.ruleContext).GetAwaiter().IsCompleted);
            Assert.False(this.ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockLowerThanAssumedValidHeight_SetSkipValidation()
        {
            this.concurrentChain = GenerateChainWithHeight(15, this.network);
            this.consensusSettings.BlockAssumedValid = this.concurrentChain.GetBlock(10).HashBlock;

            this.consensusRules = this.InitializeConsensusRules();
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            this.ruleContext.SkipValidation = false;
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(5);

            Assert.True(rule.RunAsync(this.ruleContext).GetAwaiter().IsCompleted);
            Assert.True(this.ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockEqualToThanAssumedValidHeight_SetSkipValidation()
        {
            this.concurrentChain = GenerateChainWithHeight(15, this.network);
            this.consensusSettings.BlockAssumedValid = this.concurrentChain.GetBlock(10).HashBlock;

            this.consensusRules = this.InitializeConsensusRules();
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            this.ruleContext.SkipValidation = false;
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(10);

            Assert.True(rule.RunAsync(this.ruleContext).GetAwaiter().IsCompleted);
            Assert.True(this.ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockHigherThanAssumedValidHeight_DoesNotSetSkipValidation()
        {
            this.concurrentChain = GenerateChainWithHeight(15, this.network);
            this.consensusSettings.BlockAssumedValid = this.concurrentChain.GetBlock(3).HashBlock;

            this.consensusRules = this.InitializeConsensusRules();
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            this.ruleContext.SkipValidation = false;
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(10);

            Assert.True(rule.RunAsync(this.ruleContext).GetAwaiter().IsCompleted);
            Assert.False(this.ruleContext.SkipValidation);
        }
    }
}