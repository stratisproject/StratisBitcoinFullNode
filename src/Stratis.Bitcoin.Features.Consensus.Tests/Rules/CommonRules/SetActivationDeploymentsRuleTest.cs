using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
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

        /// <summary>
        /// Checks that Pos does not set the "CheckColdStakeVerify" flag before the "ColdStakingActivationHeight" is reached.
        /// </summary>
        [Fact]
        public async Task RunAsync_ValidBlock_PosDoesNotSetCheckColdStakeVerifyFlagBeforeActivationHeightAsync()
        {
            const int ColdStakingActivationHeight = 3;

            // Update the consensus options with our required activation height.
            var network = KnownNetworks.StratisMain;
            network.Consensus.Options = new PosConsensusOptions(
                network.Consensus.Options.MaxBlockBaseSize,
                network.Consensus.Options.MaxStandardVersion,
                network.Consensus.Options.MaxStandardTxWeight,
                network.Consensus.Options.MaxBlockSigopsCost,
                ColdStakingActivationHeight
                );

            // Generate a chain one less in length to the activation height.
            this.concurrentChain = GenerateChainWithHeight(ColdStakingActivationHeight - 1, network);
            this.consensusRules = this.InitializeConsensusRules();
            this.nodeDeployments = new NodeDeployments(network, this.concurrentChain);

            // Created a block before the activation height.
            Block block = this.network.CreateBlock();
            block.AddTransaction(this.network.CreateTransaction());
            block.UpdateMerkleRoot();
            block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(5));
            block.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();

            // Verify that the block created before the activation height does not have the "CheckColdStakeVerify" flag set.
            this.ruleContext.ValidationContext.BlockToValidate = block;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.concurrentChain.Tip;

            await this.consensusRules.RegisterRule<SetActivationDeploymentsPartialValidationRule>().RunAsync(this.ruleContext);

            Assert.NotNull(this.ruleContext.Flags);
            Assert.True((this.ruleContext.Flags.ScriptFlags & ScriptVerify.CheckColdStakeVerify) == 0);            
        }

        /// <summary>
        /// Checks that Pos does sets the "CheckColdStakeVerify" flag when the "ColdStakingActivationHeight" is reached.
        /// </summary>
        [Fact]
        public async Task RunAsync_ValidBlock_PosSetsCheckColdStakeVerifyFlagAtActivationHeightAsync()
        {
            const int ColdStakingActivationHeight = 3;

            // Update the consensus options with our required activation height.
            var network = KnownNetworks.StratisMain;
            network.Consensus.Options = new PosConsensusOptions(
                network.Consensus.Options.MaxBlockBaseSize,
                network.Consensus.Options.MaxStandardVersion,
                network.Consensus.Options.MaxStandardTxWeight,
                network.Consensus.Options.MaxBlockSigopsCost,
                ColdStakingActivationHeight
                );

            // Generate a chain equal in length to the activation height.
            this.concurrentChain = GenerateChainWithHeight(ColdStakingActivationHeight, network);
            this.consensusRules = this.InitializeConsensusRules();
            this.nodeDeployments = new NodeDeployments(network, this.concurrentChain);

            // Created a block before the activation height.
            Block block = this.network.CreateBlock();
            block.AddTransaction(this.network.CreateTransaction());
            block.UpdateMerkleRoot();
            block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(5));
            block.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();

            // Verify that the block created at the activation height does have the "CheckColdStakeVerify" flag set.
            this.ruleContext.ValidationContext.BlockToValidate = block;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.concurrentChain.Tip;

            await this.consensusRules.RegisterRule<SetActivationDeploymentsPartialValidationRule>().RunAsync(this.ruleContext);

            Assert.NotNull(this.ruleContext.Flags);
            Assert.True((this.ruleContext.Flags.ScriptFlags & ScriptVerify.CheckColdStakeVerify) != 0);
        }
    }
}
