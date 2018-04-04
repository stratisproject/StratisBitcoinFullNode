using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CalculateWorkRuleTest : TestConsensusRulesUnitTestBase
    {
        public CalculateWorkRuleTest() : base()
        {
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_DoNotCheckPow_SetsNextWorkRequiredAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    ChainedBlock = this.concurrentChain.Tip
                },
                CheckPow = false,
                Consensus = this.network.Consensus
            };
            ruleContext.BlockValidationContext.Block = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain);

            var rule = this.consensusRules.RegisterRule<CalculateWorkRule>();

            await rule.RunAsync(ruleContext);

            Assert.Equal(0.465, ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_ValidPow_SetsStake_SetsNextWorkRequiredAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    ChainedBlock = this.concurrentChain.Tip
                },
                CheckPow = true,
                Consensus = this.network.Consensus
            };
            ruleContext.BlockValidationContext.Block = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain);

            var rule = this.consensusRules.RegisterRule<CalculateWorkRule>();

            await rule.RunAsync(ruleContext);

            Assert.Equal(0.465, ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsHighHashConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {                   
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                        {
                            Transactions = new List<NBitcoin.Transaction>()
                        {
                            new NBitcoin.Transaction()
                        }
                        },
                        ChainedBlock = this.concurrentChain.GetBlock(4)
                    },
                    CheckPow = true
                };

                var rule = this.consensusRules.RegisterRule<CalculateWorkRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.HighHash.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.HighHash.Message, exception.ConsensusError.Message);
        }
    }
}
