using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CalculateWorkRuleTest : TestConsensusRulesUnitTestBase
    {
        public CalculateWorkRuleTest()
        {
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_DoNotCheckPow_SetsNextWorkRequiredAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.Tip;
            this.ruleContext.BlockValidationContext.Block = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain);
            this.ruleContext.CheckPow = false;
            this.ruleContext.Consensus = this.network.Consensus;

            await this.consensusRules.RegisterRule<CalculateWorkRule>().RunAsync(this.ruleContext);

            Assert.Equal(0.465, this.ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_ValidPow_SetsStake_SetsNextWorkRequiredAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.Tip;
            this.ruleContext.BlockValidationContext.Block = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain);
            this.ruleContext.CheckPow = true;
            this.ruleContext.Consensus = this.network.Consensus;

            await this.consensusRules.RegisterRule<CalculateWorkRule>().RunAsync(this.ruleContext);

            Assert.Equal(0.465, this.ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsHighHashConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext = new BlockValidationContext()
            {
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                        {
                            new NBitcoin.Transaction()
                        }
                },
                ChainedBlock = this.concurrentChain.GetBlock(4)
            };
            this.ruleContext.CheckPow = true;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CalculateWorkRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.HighHash, exception.ConsensusError);
        }
    }
}
