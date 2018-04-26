using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderPosContextualRuleTest : TestPosConsensusRulesUnitTestBase
    {
        public BlockHeaderPosContextualRuleTest()
        {
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_HeaderVersionBelowSeven_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.SkipValidation = false;
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(3);
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Version = 6;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkTooHigh_ThrowsProofOfWorkTooHighConsensusErrorAsync()
        {
            this.ruleContext.SkipValidation = false;
            this.ruleContext.BlockValidationContext.ChainedBlock = this.concurrentChain.GetBlock(3);
            this.ruleContext.Stake = new ContextStakeInformation()
            {
                BlockStake = new BlockStake()
            };
            this.network.Consensus.LastPOWBlock = 2;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.ProofOfWorkTooHigh, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimestampTooNew_WithoutReducedDrift_ThrowsTimeTooNewConsensusErrorAsync()
        {
            this.ruleContext.SkipValidation = false;
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
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = (this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + 128 * 60 * 60) + 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimestampTooNew_WithReducedDrift_ThrowsTimeTooNewConsensusErrorAsync()
        {
            this.ruleContext.SkipValidation = false;
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
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + 16;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_BlockTimeNotTransactionTime_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            this.ruleContext.SkipValidation = false;
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
            this.ruleContext.BlockValidationContext.Block.Transactions[1].Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + 16;
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + 15;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.StakeTimeViolation, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_TransactionTimeDoesNotIncludeStakeTimestampMask_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            this.ruleContext.SkipValidation = false;
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
            this.ruleContext.BlockValidationContext.Block.Transactions[1].Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + 15;
            this.ruleContext.BlockValidationContext.ChainedBlock.Header.Time = this.ruleContext.BlockValidationContext.Block.Transactions[0].Time + 15;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.StakeTimeViolation, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BlockTimestampTooEarly_ThrowsBlockTimestampTooEarlyConsensusErrorAsync()
        {
            this.ruleContext.SkipValidation = false;
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

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooEarly, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BlockTimestampSameAsPrevious_ThrowsBlockTimestampTooEarlyConsensusErrorAsync()
        {
            this.ruleContext.SkipValidation = false;
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

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooEarly, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ValidRuleContext_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.SkipValidation = false;
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

            await this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>().RunAsync(this.ruleContext);
        }
    }
}
