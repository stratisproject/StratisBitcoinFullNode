using System.Threading.Tasks;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CheckpointsRuleTest : TestConsensusRulesUnitTestBase
    {
        public CheckpointsRuleTest() : base()
        {
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_CheckpointViolation_ThrowsCheckpointValidationConsensusErrorsExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BestBlock = new ContextBlockInformation()
                    {
                        Height = 1
                    },
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    }
                };

                this.checkpoints.Setup(c => c.CheckHardened(2, ruleContext.BlockValidationContext.Block.GetHash()))
                    .Returns(false);

                var rule = this.consensusRules.RegisterRule<CheckpointsRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.CheckpointViolation.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.CheckpointViolation.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_WithoutCheckpoints_CompletesTaskAsync()
        {
            this.consensusSettings.UseCheckpoints = false;

            var ruleContext = new RuleContext()
            {
                BestBlock = new ContextBlockInformation()
                {
                    Height = 1
                },
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
            };
            this.checkpoints.Setup(c => c.CheckHardened(2, ruleContext.BlockValidationContext.Block.GetHash()))
                .Returns(true);

            var rule = this.consensusRules.RegisterRule<CheckpointsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_WithCheckpoints_SetsSkipValidation_ChainedBlockHeightBelowLastCheckpointHeight_SetsSkipValidationToTrueAsync()
        {
            this.consensusSettings.UseCheckpoints = true;

            var ruleContext = new RuleContext()
            {
                SkipValidation = false,
                BestBlock = new ContextBlockInformation()
                {
                    Height = 1
                },
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block(),
                    ChainedBlock = this.concurrentChain.GetBlock(5)
                },
            };
            this.checkpoints.Setup(c => c.CheckHardened(2, ruleContext.BlockValidationContext.Block.GetHash()))
                .Returns(true);
            this.checkpoints.Setup(c => c.GetLastCheckpointHeight())
                .Returns(10);

            var rule = this.consensusRules.RegisterRule<CheckpointsRule>();

            await rule.RunAsync(ruleContext);

            Assert.True(ruleContext.SkipValidation);
        }

        [Fact]
        public async Task RunAsync_WithCheckpoints_SetsSkipValidation_ChainedBlockHeightSameAsLastCheckpointHeight_SetsSkipValidationToTrueAsync()
        {
            this.consensusSettings.UseCheckpoints = true;

            var ruleContext = new RuleContext()
            {
                SkipValidation = false,
                BestBlock = new ContextBlockInformation()
                {
                    Height = 1
                },
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block(),
                    ChainedBlock = this.concurrentChain.GetBlock(5)
                },
            };
            this.checkpoints.Setup(c => c.CheckHardened(2, ruleContext.BlockValidationContext.Block.GetHash()))
                .Returns(true);
            this.checkpoints.Setup(c => c.GetLastCheckpointHeight())
                .Returns(5);

            var rule = this.consensusRules.RegisterRule<CheckpointsRule>();

            await rule.RunAsync(ruleContext);

            Assert.True(ruleContext.SkipValidation);
        }

        [Fact]
        public async Task RunAsync_WithCheckpoints_SetsSkipValidation_ChainedBlockHeightAboveLastCheckpointHeight_SetsSkipValidationToFalseAsync()
        {
            this.consensusSettings.UseCheckpoints = true;

            var ruleContext = new RuleContext()
            {
                SkipValidation = false,
                BestBlock = new ContextBlockInformation()
                {
                    Height = 1
                },
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block(),
                    ChainedBlock = this.concurrentChain.GetBlock(5)
                },
            };
            this.checkpoints.Setup(c => c.CheckHardened(2, ruleContext.BlockValidationContext.Block.GetHash()))
                .Returns(true);
            this.checkpoints.Setup(c => c.GetLastCheckpointHeight())
                .Returns(3);

            var rule = this.consensusRules.RegisterRule<CheckpointsRule>();

            await rule.RunAsync(ruleContext);

            Assert.False(ruleContext.SkipValidation);
        }
    }
}