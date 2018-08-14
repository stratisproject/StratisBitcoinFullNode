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
        public void Run_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsHighHashConsensusErrorException()
        {
            Block block = this.network.CreateBlock();
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                Block = block,
                ChainTipToExtend = this.concurrentChain.GetBlock(4)
            };
            this.ruleContext.MinedBlock = false;

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckDifficultyPowRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.HighHash, exception.ConsensusError);
        }

        [Fact]
        public void Run_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsBadDiffBitsConsensusErrorException()
        {
            Block block = this.network.CreateBlock();
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                Block = block,
                ChainTipToExtend = this.concurrentChain.GetBlock(0)
            };
            this.ruleContext.MinedBlock = true;

            block.Header.Bits = this.ruleContext.ValidationContext.ChainTipToExtend.GetWorkRequired(this.network.Consensus) + 1;

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckDifficultyPowRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadDiffBits, exception.ConsensusError);
        }
    }
}