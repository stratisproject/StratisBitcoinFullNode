using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosCheckColdStakeVerifyFlagTest : TestPosConsensusRulesUnitTestBase
    {
        /// <summary>
        /// Checks that <see cref="DeploymentFlags.DeploymentFlags(ChainedHeader, ThresholdState[], IConsensus, ConcurrentChain)"/> only sets the
        /// <see cref="ScriptVerify.CheckColdStakeVerify"/> flag once the <see cref="PosConsensusOptions.ColdStakingActivationHeight"/> is reached.
        /// </summary>
        [Fact]
        public async Task PosDoesNotSetCheckColdStakeVerifyFlagBeforeActivationHeightAsync()
        {
            const int ColdStakingActivationHeight = 3;

            // Update the consensus options with our required activation height.
            this.network.Consensus.Options = new PosConsensusOptions(
                this.network.Consensus.Options.MaxBlockBaseSize,
                this.network.Consensus.Options.MaxStandardVersion,
                this.network.Consensus.Options.MaxStandardTxWeight,
                this.network.Consensus.Options.MaxBlockSigopsCost,
                ColdStakingActivationHeight
                );

            // Generate a chain two less in length to the activation height.
            this.concurrentChain = GenerateChainWithHeight(ColdStakingActivationHeight - 2, this.network);
            this.consensusRules = this.InitializeConsensusRules();
            this.nodeDeployments = new NodeDeployments(this.network, this.concurrentChain);

            // Create a block before the activation height.
            Block block1 = this.network.CreateBlock();
            block1.AddTransaction(this.network.CreateTransaction());
            block1.UpdateMerkleRoot();
            block1.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            block1.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block1.Header.Nonce = RandomUtils.GetUInt32();

            // Construct new chained header and append to chain.
            this.concurrentChain.SetTip(block1.Header);
            DeploymentFlags flags1 = this.nodeDeployments.GetFlags(this.concurrentChain.Tip);

            // Add another block.
            Block block2 = this.network.CreateBlock();
            block2.AddTransaction(this.network.CreateTransaction());
            block2.UpdateMerkleRoot();
            block2.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(5));
            block2.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block2.Header.Nonce = RandomUtils.GetUInt32();

            // Construct new chained header and append to chain.
            this.concurrentChain.SetTip(block2.Header);
            DeploymentFlags flags2 = this.nodeDeployments.GetFlags(this.concurrentChain.Tip);

            // Verify that the block created before the activation height does not have the flag set.
            Assert.Equal(0, (int)(flags1.ScriptFlags & ScriptVerify.CheckColdStakeVerify));

            // Verify that the block created at the activation height does have the flag set.
            Assert.NotEqual(0, (int)(flags2.ScriptFlags & ScriptVerify.CheckColdStakeVerify));
        }
    }
}
