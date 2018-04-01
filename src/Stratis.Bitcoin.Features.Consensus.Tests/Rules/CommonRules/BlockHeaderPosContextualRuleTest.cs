using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderPosContextualRuleTest : TestPosConsensusRulesUnitTestBase
    {
        public BlockHeaderPosContextualRuleTest() : base()
        {
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
        }

        [Fact]
        public async Task RunAsync_HeaderVersionBelowSeven_ThrowsBadVersionConsensusErrorAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    SkipValidation = false,
                    BlockValidationContext = new BlockValidationContext()
                    {
                        ChainedBlock = this.concurrentChain.GetBlock(3)
                    }
                };
                ruleContext.BlockValidationContext.ChainedBlock.Header.Version = 6;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkTooHigh_ThrowsProofOfWorkTooHighConsensusErrorAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    SkipValidation = false,
                    BlockValidationContext = new BlockValidationContext()
                    {
                        ChainedBlock = this.concurrentChain.GetBlock(3)
                    },
                    Stake = new ContextStakeInformation()
                    {
                        BlockStake = new NBitcoin.BlockStake()
                    }
                };
                this.network.Consensus.LastPOWBlock = 2;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.ProofOfWorkTooHeigh.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.ProofOfWorkTooHeigh.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_TimestampTooNew_WithoutReducedDrift_ThrowsTimeTooNewConsensusErrorAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    SkipValidation = false,
                    Stake = new ContextStakeInformation()
                    {
                        BlockStake = new NBitcoin.BlockStake()
                    },
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                        {
                            Transactions = new List<NBitcoin.Transaction>()
                            {
                                new NBitcoin.Transaction()
                                {
                                    Time = (uint)(PosConsensusValidator.DriftingBugFixTimestamp - 100)
                                }
                            }
                        },
                        ChainedBlock = this.concurrentChain.GetBlock(3)
                    },
                };
                this.network.Consensus.LastPOWBlock = 12500;
                ruleContext.BlockValidationContext.ChainedBlock.Header.Time = (ruleContext.BlockValidationContext.Block.Transactions[0].Time + 128 * 60 * 60) + 1;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.TimeTooNew.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.TimeTooNew.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_TimestampTooNew_WithReducedDrift_ThrowsTimeTooNewConsensusErrorAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    SkipValidation = false,
                    Stake = new ContextStakeInformation()
                    {
                        BlockStake = new NBitcoin.BlockStake()
                    },
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                        {
                            Transactions = new List<NBitcoin.Transaction>()
                            {
                                new NBitcoin.Transaction()
                                {
                                    Time = (uint)(PosConsensusValidator.DriftingBugFixTimestamp + 100)
                                }
                            }
                        },
                        ChainedBlock = this.concurrentChain.GetBlock(3)
                    },
                };
                this.network.Consensus.LastPOWBlock = 12500;
                ruleContext.BlockValidationContext.ChainedBlock.Header.Time = ruleContext.BlockValidationContext.Block.Transactions[0].Time + 16;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.TimeTooNew.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.TimeTooNew.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_BlockTimeNotTransactionTime_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    SkipValidation = false,
                    Stake = new ContextStakeInformation()
                    {
                        BlockStake = new NBitcoin.BlockStake()
                        {
                            Flags = NBitcoin.BlockFlag.BLOCK_PROOF_OF_STAKE
                        }
                    },
                    BlockValidationContext = new BlockValidationContext()
                    {
                        ChainedBlock = this.concurrentChain.GetBlock(3),
                        Block = new NBitcoin.Block()
                        {
                            Transactions = new List<NBitcoin.Transaction>()
                            {
                                new NBitcoin.Transaction(){ Time = (uint)PosConsensusValidator.DriftingBugFixTimestamp },
                                new NBitcoin.Transaction(){ }
                            }
                        },
                    },
                };
                this.network.Consensus.LastPOWBlock = 12500;
                ruleContext.BlockValidationContext.Block.Transactions[1].Time = ruleContext.BlockValidationContext.Block.Transactions[0].Time + 16;
                ruleContext.BlockValidationContext.ChainedBlock.Header.Time = ruleContext.BlockValidationContext.Block.Transactions[0].Time + 15;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.StakeTimeViolation.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.StakeTimeViolation.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_TransactionTimeDoesNotIncludeStakeTimestampMask_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    SkipValidation = false,
                    Stake = new ContextStakeInformation()
                    {
                        BlockStake = new NBitcoin.BlockStake()
                        {
                            Flags = NBitcoin.BlockFlag.BLOCK_PROOF_OF_STAKE
                        }
                    },
                    BlockValidationContext = new BlockValidationContext()
                    {
                        ChainedBlock = this.concurrentChain.GetBlock(3),
                        Block = new NBitcoin.Block()
                        {
                            Transactions = new List<NBitcoin.Transaction>()
                            {
                                new NBitcoin.Transaction(){ Time = (uint)PosConsensusValidator.DriftingBugFixTimestamp },
                                new NBitcoin.Transaction(){ }
                            }
                        },
                    },
                };
                this.network.Consensus.LastPOWBlock = 12500;
                ruleContext.BlockValidationContext.Block.Transactions[1].Time = ruleContext.BlockValidationContext.Block.Transactions[0].Time + 15;
                ruleContext.BlockValidationContext.ChainedBlock.Header.Time = ruleContext.BlockValidationContext.Block.Transactions[0].Time + 15;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.StakeTimeViolation.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.StakeTimeViolation.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BlockTimestampTooEarly_ThrowsBlockTimestampTooEarlyConsensusErrorAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    SkipValidation = false,
                    Stake = new ContextStakeInformation()
                    {
                        BlockStake = new NBitcoin.BlockStake()
                        {
                            Flags = NBitcoin.BlockFlag.BLOCK_PROOF_OF_STAKE
                        }
                    },
                    BlockValidationContext = new BlockValidationContext()
                    {
                        ChainedBlock = this.concurrentChain.GetBlock(3),
                        Block = new NBitcoin.Block()
                        {
                            Transactions = new List<NBitcoin.Transaction>()
                            {
                                new NBitcoin.Transaction(){ },
                                new NBitcoin.Transaction(){ }
                            }
                        },
                    },
                };
                this.network.Consensus.LastPOWBlock = 12500;

                // time before previous block.
                var previousBlockHeaderTime = ruleContext.BlockValidationContext.ChainedBlock.Previous.Header.Time;
                ruleContext.BlockValidationContext.Block.Transactions[0].Time = previousBlockHeaderTime;                
                ruleContext.BlockValidationContext.Block.Transactions[1].Time = previousBlockHeaderTime - 32;
                ruleContext.BlockValidationContext.ChainedBlock.Header.Time = previousBlockHeaderTime - 32;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BlockTimestampTooEarly.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BlockTimestampTooEarly.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_ValidRuleContext_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                SkipValidation = false,
                Stake = new ContextStakeInformation()
                {
                    BlockStake = new NBitcoin.BlockStake()
                    {
                        Flags = NBitcoin.BlockFlag.BLOCK_PROOF_OF_STAKE
                    }
                },
                BlockValidationContext = new BlockValidationContext()
                {
                    ChainedBlock = this.concurrentChain.GetBlock(3),
                    Block = new NBitcoin.Block()
                    {
                        Transactions = new List<NBitcoin.Transaction>()
                            {
                                new NBitcoin.Transaction(){ },
                                new NBitcoin.Transaction(){ }
                            }
                    },
                },
            };
            this.network.Consensus.LastPOWBlock = 12500;

            // time after previous block.
            var previousBlockHeaderTime = ruleContext.BlockValidationContext.ChainedBlock.Previous.Header.Time;
            ruleContext.BlockValidationContext.Block.Transactions[0].Time = previousBlockHeaderTime + 62;
            ruleContext.BlockValidationContext.Block.Transactions[1].Time = previousBlockHeaderTime + 64;
            ruleContext.BlockValidationContext.ChainedBlock.Header.Time = previousBlockHeaderTime + 64;

            var rule = this.consensusRules.RegisterRule<BlockHeaderPosContextualRule>();
            await rule.RunAsync(ruleContext);
        }
    }
}
