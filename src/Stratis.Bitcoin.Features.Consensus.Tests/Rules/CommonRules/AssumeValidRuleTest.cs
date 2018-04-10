using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class AssumeValidRuleTest : ConsensusRuleUnitTestBase
    {
        public AssumeValidRuleTest() : base()
        {
        }

        [Fact]
        public void Initialize_CheckpointsRuleInConsensusRules_DoesNotThrowException()
        {
            this.consensusRules.RegisterRule<CheckpointsRule>();

            this.consensusRules.RegisterRule<AssumeValidRule>();
        }

        [Fact]
        public void Initialize_CheckpointsRuleNotInConsensusRules_ThrowExceptions()
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

            Assert.True(rule.RunAsync(new RuleContext() { SkipValidation = true }).GetAwaiter().IsCompleted);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidNotSetOnConsensus_ReturnsCompletedTask()
        {
            this.consensusRules.ConsensusSettings.BlockAssumedValid = null;
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            var ruleContext = new RuleContext() { SkipValidation = false };

            Assert.True(rule.RunAsync(ruleContext).GetAwaiter().IsCompleted);
            Assert.False(ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockNotOnChain_DoesNotSetSkipValidation()
        {
            this.consensusRules.ConsensusSettings.BlockAssumedValid = new NBitcoin.uint256(25);
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            var ruleContext = new RuleContext() { SkipValidation = false };

            Assert.True(rule.RunAsync(ruleContext).GetAwaiter().IsCompleted);
            Assert.False(ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockLowerThanAssumedValidHeight_SetSkipValidation()
        {
            this.concurrentChain = GenerateChainWithHeight(15, this.network);
            this.consensusSettings.BlockAssumedValid = this.concurrentChain.GetBlock(10).HashBlock;

            this.consensusRules = this.InitializeConsensusRules();
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            var ruleContext = new RuleContext()
            {
                SkipValidation = false,
                BlockValidationContext = new BlockValidationContext()
                {
                    ChainedBlock = this.concurrentChain.GetBlock(5)
                }
            };

            Assert.True(rule.RunAsync(ruleContext).GetAwaiter().IsCompleted);
            Assert.True(ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockEqualToThanAssumedValidHeight_SetSkipValidation()
        {
            this.concurrentChain = GenerateChainWithHeight(15, this.network);
            this.consensusSettings.BlockAssumedValid = this.concurrentChain.GetBlock(10).HashBlock;

            this.consensusRules = this.InitializeConsensusRules();
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            var ruleContext = new RuleContext()
            {
                SkipValidation = false,
                BlockValidationContext = new BlockValidationContext()
                {
                    ChainedBlock = this.concurrentChain.GetBlock(10)
                }
            };

            Assert.True(rule.RunAsync(ruleContext).GetAwaiter().IsCompleted);
            Assert.True(ruleContext.SkipValidation);
        }

        [Fact]
        public void RunAsync_DoNotSkipValidation_BlockAssumedValidSetOnConsensus_BlockHigherThanAssumedValidHeight_DoesNotSetSkipValidation()
        {
            this.concurrentChain = GenerateChainWithHeight(15, this.network);
            this.consensusSettings.BlockAssumedValid = this.concurrentChain.GetBlock(3).HashBlock;

            this.consensusRules = this.InitializeConsensusRules();
            this.consensusRules.RegisterRule<CheckpointsRule>();

            var rule = this.consensusRules.RegisterRule<AssumeValidRule>();
            var ruleContext = new RuleContext()
            {
                SkipValidation = false,
                BlockValidationContext = new BlockValidationContext()
                {
                    ChainedBlock = this.concurrentChain.GetBlock(10)
                }
            };

            Assert.True(rule.RunAsync(ruleContext).GetAwaiter().IsCompleted);
            Assert.False(ruleContext.SkipValidation);
        }

        private static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }
    }
}
