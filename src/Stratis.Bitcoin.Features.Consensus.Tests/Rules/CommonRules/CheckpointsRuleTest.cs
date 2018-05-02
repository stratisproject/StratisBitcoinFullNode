using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CheckpointsRuleTest : TestConsensusRulesUnitTestBase
    {
        public CheckpointsRuleTest()
        {
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_CheckpointViolation_ThrowsCheckpointValidationConsensusErrorsExceptionAsync()
        {
            this.ruleContext.BestBlock = new ContextBlockInformation() { Height = 1 };
            this.ruleContext.BlockValidationContext.Block = new Block();

            this.checkpoints.Setup(c => c.CheckHardened(2, this.ruleContext.BlockValidationContext.Block.GetHash()))
                .Returns(false);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckpointsRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.CheckpointViolation, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_WithoutCheckpoints_CompletesTaskAsync()
        {
            this.consensusSettings.UseCheckpoints = false;

            this.ruleContext.BestBlock = new ContextBlockInformation() { Height = 1 };
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.checkpoints.Setup(c => c.CheckHardened(2, this.ruleContext.BlockValidationContext.Block.GetHash()))
                .Returns(true);

            await this.consensusRules.RegisterRule<CheckpointsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_WithCheckpoints_SetsSkipValidation_ChainedBlockHeightBelowLastCheckpointHeight_SetsSkipValidationToTrueAsync()
        {
            this.consensusSettings.UseCheckpoints = true;

            this.ruleContext.SkipValidation = false;
            this.ruleContext.BestBlock = new ContextBlockInformation() { Height = 1 };
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(5);

            this.checkpoints.Setup(c => c.CheckHardened(2, this.ruleContext.BlockValidationContext.Block.GetHash()))
                .Returns(true);
            this.checkpoints.Setup(c => c.GetLastCheckpointHeight())
                .Returns(10);

            await this.consensusRules.RegisterRule<CheckpointsRule>().RunAsync(this.ruleContext);

            Assert.True(this.ruleContext.SkipValidation);
        }

        [Fact]
        public async Task RunAsync_WithCheckpoints_SetsSkipValidation_ChainedBlockHeightSameAsLastCheckpointHeight_SetsSkipValidationToTrueAsync()
        {
            this.consensusSettings.UseCheckpoints = true;

            this.ruleContext.SkipValidation = false;
            this.ruleContext.BestBlock = new ContextBlockInformation() { Height = 1 };
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(5);

            this.checkpoints.Setup(c => c.CheckHardened(2, this.ruleContext.BlockValidationContext.Block.GetHash()))
                .Returns(true);
            this.checkpoints.Setup(c => c.GetLastCheckpointHeight())
                .Returns(5);

            await this.consensusRules.RegisterRule<CheckpointsRule>().RunAsync(this.ruleContext);

            Assert.True(this.ruleContext.SkipValidation);
        }

        [Fact]
        public async Task RunAsync_WithCheckpoints_SetsSkipValidation_ChainedBlockHeightAboveLastCheckpointHeight_SetsSkipValidationToFalseAsync()
        {
            this.consensusSettings.UseCheckpoints = true;

            this.ruleContext.SkipValidation = false;
            this.ruleContext.BestBlock = new ContextBlockInformation() { Height = 1 };
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(5);

            this.checkpoints.Setup(c => c.CheckHardened(2, this.ruleContext.BlockValidationContext.Block.GetHash()))
                .Returns(true);
            this.checkpoints.Setup(c => c.GetLastCheckpointHeight())
                .Returns(3);

            await this.consensusRules.RegisterRule<CheckpointsRule>().RunAsync(this.ruleContext);

            Assert.False(this.ruleContext.SkipValidation);
        }
    }
}