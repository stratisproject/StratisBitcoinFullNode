using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;
using static NBitcoin.Transaction;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class SetActivationDeploymentsRuleTest : TestConsensusRulesUnitTestBase
    {
        public SetActivationDeploymentsRuleTest()
        {
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_ValidBlock_SetsConsensusFlagsAsync()
        {
            this.nodeDeployments = new NodeDeployments(this.network, this.concurrentChain);
            this.consensusRules = this.InitializeConsensusRules();

            Block block = this.network.CreateBlock();
            block.AddTransaction(this.network.CreateTransaction());
            block.UpdateMerkleRoot();
            block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(5));
            block.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();

            this.ruleContext.ValidationContext.BlockToValidate = block;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.concurrentChain.Tip;

            await this.consensusRules.RegisterRule<SetActivationDeploymentsPartialValidationRule>().RunAsync(this.ruleContext);

            Assert.NotNull(this.ruleContext.Flags);
            Assert.True(this.ruleContext.Flags.EnforceBIP30);
            Assert.False(this.ruleContext.Flags.EnforceBIP34);
            Assert.Equal(LockTimeFlags.None, this.ruleContext.Flags.LockTimeFlags);
            Assert.Equal(ScriptVerify.Mandatory, this.ruleContext.Flags.ScriptFlags);
        }
    }
}
