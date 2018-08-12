using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CalculateWorkRuleTest : TestConsensusRulesUnitTestBase
    {
        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsHighHashConsensusErrorExceptionAsync()
        {
            Block block = this.network.CreateBlock();
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                Block = block,
                ChainTipToExtend = this.concurrentChain.GetBlock(4)
            };
            this.ruleContext.MinedBlock = false;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckDifficultyPowRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.HighHash, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsBadDiffBitsConsensusErrorExceptionAsync()
        {
            Block block = this.network.CreateBlock();
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                Block = block,
                ChainTipToExtend = this.concurrentChain.GetBlock(0)
            };
            this.ruleContext.MinedBlock = true;

            block.Header.Bits = this.ruleContext.ValidationContext.ChainTipToExtend.GetWorkRequired(this.network.Consensus) + 1;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckDifficultyPowRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadDiffBits, exception.ConsensusError);
        }
    }
}