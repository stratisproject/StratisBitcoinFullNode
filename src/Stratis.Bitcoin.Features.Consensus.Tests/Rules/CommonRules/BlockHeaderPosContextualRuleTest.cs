using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
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
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(1);
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Version = MinimalHeaderVersion - 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkTooHigh_ThrowsProofOfWorkTooHighConsensusErrorAsync()
        {
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(3);
            this.ruleContext.Stake = new ContextStakeInformation()
            {
                BlockStake = new BlockStake()
            };
            this.network.Consensus.LastPOWBlock = 2;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.ProofOfWorkTooHigh, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimestampTooNew_WithoutReducedDrift_ThrowsTimeTooNewConsensusErrorAsync()
        {
            this.ruleContext.Stake = new ContextStakeInformation()
            {
                BlockStake = new BlockStake()
            };
            this.ruleContext.BlockValidationContext = new BlockValidationContext()
            {
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                    {
                        new Transaction()
                        {
                            Time = (uint)(PosConsensusValidator.DriftingBugFixTimestamp - 100)
                        }
                    }
                },
                ChainedBlock = this.concurrentChain.GetBlock(3)
            };
            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + MaxFutureDriftBeforeHardFork + 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimestampTooNew_WithReducedDrift_ThrowsTimeTooNewConsensusErrorAsync()
        {
            this.ruleContext.Stake = new ContextStakeInformation()
            {
                BlockStake = new BlockStake()
            };
            this.ruleContext.BlockValidationContext = new BlockValidationContext()
            {
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                    {
                        new Transaction()
                        {
                            Time = (uint)(PosConsensusValidator.DriftingBugFixTimestamp + 100)
                        }
                    }
                },
                ChainedBlock = this.concurrentChain.GetBlock(3)
            };

            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork + 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_BlockTimeNotTransactionTime_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            this.ruleContext.Stake = new ContextStakeInformation()
            {
                BlockStake = new BlockStake()
                {
                    Flags = BlockFlag.BLOCK_PROOF_OF_STAKE
                }
            };
            this.ruleContext.BlockValidationContext = new BlockValidationContext()
            {
                ChainedBlock = this.concurrentChain.GetBlock(3),
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                    {
                        new Transaction(){ Time = (uint)PosConsensusValidator.DriftingBugFixTimestamp },
                        new Transaction(){ }
                    }
                },
            };
            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.BlockValidationContext.Block.Transactions[1].Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork + 1;
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.StakeTimeViolation, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_TransactionTimeDoesNotIncludeStakeTimestampMask_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            this.ruleContext.Stake = new ContextStakeInformation()
            {
                BlockStake = new BlockStake()
                {
                    Flags = BlockFlag.BLOCK_PROOF_OF_STAKE
                }
            };
            this.ruleContext.BlockValidationContext = new BlockValidationContext()
            {
                ChainedBlock = this.concurrentChain.GetBlock(3),
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                    {
                        new Transaction(){ Time = (uint)PosConsensusValidator.DriftingBugFixTimestamp },
                        new Transaction(){ }
                    }
                },
            };
            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.BlockValidationContext.Block.Transactions[1].Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork;
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + MaxFutureDriftAfterHardFork;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.StakeTimeViolation, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BlockTimestampTooEarly_ThrowsBlockTimestampTooEarlyConsensusErrorAsync()
        {
            this.ruleContext.Stake = new ContextStakeInformation()
            {
                BlockStake = new BlockStake()
                {
                    Flags = BlockFlag.BLOCK_PROOF_OF_STAKE
                }
            };
            this.ruleContext.BlockValidationContext = new BlockValidationContext()
            {
                ChainedBlock = this.concurrentChain.GetBlock(3),
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                    {
                        new Transaction(){ },
                        new Transaction(){ }
                    }
                },
            };
            this.network.Consensus.LastPOWBlock = 12500;

            // time before previous block.
            var previousBlockHeaderTime = this.ruleContext.BlockValidationContext.ChainedBlock.Previous.Header.Time;
            this.ruleContext.BlockValidationContext.Block.Transactions[0].Time = previousBlockHeaderTime;
            this.ruleContext.BlockValidationContext.Block.Transactions[1].Time = previousBlockHeaderTime - 32;
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = previousBlockHeaderTime - 32;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooEarly, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BlockTimestampSameAsPrevious_ThrowsBlockTimestampTooEarlyConsensusErrorAsync()
        {
            this.ruleContext.Stake = new ContextStakeInformation()
            {
                BlockStake = new BlockStake()
                {
                    Flags = BlockFlag.BLOCK_PROOF_OF_STAKE
                }
            };
            this.ruleContext.BlockValidationContext = new BlockValidationContext()
            {
                ChainedBlock = this.concurrentChain.GetBlock(3),
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                    {
                        new Transaction(){ },
                        new Transaction(){ }
                    }
                },
            };
            this.network.Consensus.LastPOWBlock = 12500;

            // time same as previous block.
            var previousBlockHeaderTime = this.ruleContext.BlockValidationContext.ChainedBlock.Previous.Header.Time;
            this.ruleContext.BlockValidationContext.Block.Transactions[0].Time = previousBlockHeaderTime;
            this.ruleContext.BlockValidationContext.Block.Transactions[1].Time = previousBlockHeaderTime;
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = previousBlockHeaderTime;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooEarly, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ValidRuleContext_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.Stake = new ContextStakeInformation()
            {
                BlockStake = new BlockStake()
                {
                    Flags = BlockFlag.BLOCK_PROOF_OF_STAKE
                }
            };
            this.ruleContext.BlockValidationContext = new BlockValidationContext()
            {
                ChainedBlock = this.concurrentChain.GetBlock(3),
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                    {
                        new Transaction(){ },
                        new Transaction(){ }
                    }
                },
            };
            this.network.Consensus.LastPOWBlock = 12500;

            // time after previous block.
            var previousBlockHeaderTime = this.ruleContext.BlockValidationContext.ChainedBlock.Previous.Header.Time;
            this.ruleContext.BlockValidationContext.Block.Transactions[0].Time = previousBlockHeaderTime + 62;
            this.ruleContext.BlockValidationContext.Block.Transactions[1].Time = previousBlockHeaderTime + 64;
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = previousBlockHeaderTime + 64;

            await this.rule.RunAsync(this.ruleContext);
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
