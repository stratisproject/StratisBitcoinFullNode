using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderPosContextualRuleTest : PosConsensusRuleUnitTestBase
    {      
        private BlockHeaderPosContextualRule rule;
        private const int MaxFutureDriftBeforeHardFork = 128 * 60 * 60;
        private const int MaxFutureDriftAfterHardFork = 15;

        public BlockHeaderPosContextualRuleTest()
        {           
            AddBlocksToChain(this.concurrentChain, 5);
            this.rule = this.CreateRule();
        }

        [Fact]
        public async Task RunAsync_HeaderVersionBelowMinimalHeaderVersion_ThrowsBadVersionConsensusErrorAsync()
        {
            int MinimalHeaderVersion = 7;
            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.GetBlock(1);
            this.ruleContext.ValidationContext.ChainedHeader.Header.Version = MinimalHeaderVersion - 1;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkTooHigh_ThrowsProofOfWorkTooHighConsensusErrorAsync()
        {
            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.GetBlock(3);
            this.SetBlockStake();
            this.network.Consensus.LastPOWBlock = 2;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.ProofOfWorkTooHigh, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimestampTooNew_WithoutReducedDrift_ThrowsTimeTooNewConsensusErrorAsync()
        {
            this.SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.Block = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());
            this.ruleContext.ValidationContext.Block.Transactions[0].Time = (uint) (PosFutureDriftRule.DriftingBugFixTimestamp - 100);
            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.GetBlock(3);

            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.ValidationContext.ChainedHeader.Header.Time = this.ruleContext.ValidationContext.Block.Transactions[0].Time + MaxFutureDriftBeforeHardFork + 1;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimestampTooNew_WithReducedDrift_ThrowsTimeTooNewConsensusErrorAsync()
        {
            this.SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.Block = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());
            this.ruleContext.ValidationContext.Block.Transactions[0].Time = (uint)(PosFutureDriftRule.DriftingBugFixTimestamp + 100);
            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.GetBlock(3);

            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.ValidationContext.ChainedHeader.Header.Time = this.ruleContext.ValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork + 1;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_BlockTimeNotTransactionTime_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            this.SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.Block = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());
            this.ruleContext.ValidationContext.Block.Transactions[0].Time = (uint)(PosFutureDriftRule.DriftingBugFixTimestamp);

            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.GetBlock(3);
            
            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.ValidationContext.Block.Transactions[1].Time = this.ruleContext.ValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork + 1;
            this.ruleContext.ValidationContext.ChainedHeader.Header.Time = this.ruleContext.ValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.StakeTimeViolation, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_TransactionTimeDoesNotIncludeStakeTimestampMask_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            this.SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.Block = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());
            this.ruleContext.ValidationContext.Block.Transactions[0].Time = (uint)(PosFutureDriftRule.DriftingBugFixTimestamp);

            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.GetBlock(3);
            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.ValidationContext.Block.Transactions[1].Time = this.ruleContext.ValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork;
            this.ruleContext.ValidationContext.ChainedHeader.Header.Time = this.ruleContext.ValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.StakeTimeViolation, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BlockTimestampTooEarly_ThrowsBlockTimestampTooEarlyConsensusErrorAsync()
        {
            this.SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.Block = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());

            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.GetBlock(3);
            this.network.Consensus.LastPOWBlock = 12500;

            // time before previous block.
            uint previousBlockHeaderTime = this.ruleContext.ValidationContext.ChainedHeader.Previous.Header.Time;
            this.ruleContext.ValidationContext.Block.Transactions[0].Time = previousBlockHeaderTime;
            this.ruleContext.ValidationContext.Block.Transactions[1].Time = previousBlockHeaderTime - 32;
            this.ruleContext.ValidationContext.ChainedHeader.Header.Time = previousBlockHeaderTime - 32;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooEarly, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BlockTimestampSameAsPrevious_ThrowsBlockTimestampTooEarlyConsensusErrorAsync()
        {
            this.SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.Block = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());

            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.GetBlock(3);
            this.network.Consensus.LastPOWBlock = 12500;

            // time same as previous block.
            uint previousBlockHeaderTime = this.ruleContext.ValidationContext.ChainedHeader.Previous.Header.Time;
            this.ruleContext.ValidationContext.Block.Transactions[0].Time = previousBlockHeaderTime;
            this.ruleContext.ValidationContext.Block.Transactions[1].Time = previousBlockHeaderTime;
            this.ruleContext.ValidationContext.ChainedHeader.Header.Time = previousBlockHeaderTime;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooEarly, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ValidRuleContext_DoesNotThrowExceptionAsync()
        {
            this.SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.Block = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());
            this.ruleContext.ValidationContext.Block.Transactions.Add(this.network.Consensus.ConsensusFactory.CreateTransaction());

            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.GetBlock(3);
            this.network.Consensus.LastPOWBlock = 12500;

            // time after previous block.
            uint previousBlockHeaderTime = this.ruleContext.ValidationContext.ChainedHeader.Previous.Header.Time;
            this.ruleContext.ValidationContext.Block.Transactions[0].Time = previousBlockHeaderTime + 62;
            this.ruleContext.ValidationContext.Block.Transactions[1].Time = previousBlockHeaderTime + 64;
            this.ruleContext.ValidationContext.ChainedHeader.Header.Time = previousBlockHeaderTime + 64;

            await this.rule.RunAsync(this.ruleContext);
        }

        private void SetBlockStake(BlockFlag flg)
        {
            (this.ruleContext as PosRuleContext).BlockStake = new BlockStake()
            {
                Flags = flg
            };
        }

        private void SetBlockStake()
        {
            (this.ruleContext as PosRuleContext).BlockStake = new BlockStake();
        }

        private BlockHeaderPosContextualRule CreateRule()
        {
            return new BlockHeaderPosContextualRule()
            {
                Logger = this.logger.Object,
                Parent = new TestPosConsensusRules(this.network, this.loggerFactory.Object, this.dateTimeProvider.Object, this.concurrentChain, this.nodeDeployments, this.consensusSettings, this.checkpoints.Object, this.coinView.Object, this.lookaheadBlockPuller.Object, this.stakeChain.Object, this.stakeValidator.Object)
            };
        }
    }
}
