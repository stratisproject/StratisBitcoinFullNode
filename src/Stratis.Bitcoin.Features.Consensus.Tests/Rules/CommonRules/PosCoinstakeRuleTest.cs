using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosCoinstakeRuleTest : TestPosConsensusRulesUnitTestBase
    {
        public PosCoinstakeRuleTest() : base()
        {
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_CoinBaseNotEmpty_NoOutputsOnTransaction_ThrowsBadStakeBlockConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    }
                };

                ruleContext.BlockValidationContext.Block.Transactions.Add(new Transaction());

                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn()
                {
                    PrevOut = new OutPoint(new uint256(15), 1),
                    ScriptSig = new Script()
                });
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                Assert.True(BlockStake.IsProofOfStake(ruleContext.BlockValidationContext.Block));

                var rule = this.consensusRules.RegisterRule<PosCoinstakeRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadStakeBlock.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadStakeBlock.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_CoinBaseNotEmpty_TransactionNotEmpty_ThrowsBadStakeBlockConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    }
                };

                var transaction = new Transaction();
                transaction.Outputs.Add(new TxOut(new Money(1), (IDestination)null));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                transaction = new Transaction();
                transaction.Inputs.Add(new TxIn()
                {
                    PrevOut = new OutPoint(new uint256(15), 1),
                    ScriptSig = new Script()
                });
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                Assert.True(BlockStake.IsProofOfStake(ruleContext.BlockValidationContext.Block));

                var rule = this.consensusRules.RegisterRule<PosCoinstakeRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadStakeBlock.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadStakeBlock.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_MultipleCoinStakeAfterSecondTransaction_ThrowsBadMultipleCoinstakeConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    }
                };

                var transaction = new Transaction();
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                transaction = new Transaction();
                transaction.Inputs.Add(new TxIn()
                {
                    PrevOut = new OutPoint(new uint256(15), 1),
                    ScriptSig = new Script()
                });
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                Assert.True(BlockStake.IsProofOfStake(ruleContext.BlockValidationContext.Block));

                var rule = this.consensusRules.RegisterRule<PosCoinstakeRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadMultipleCoinstake.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadMultipleCoinstake.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_TransactionTimestampAfterBlockTimeStamp_ThrowsBlockTimeBeforeTrxConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    }
                };

                var transaction = new Transaction();
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                transaction = new Transaction();
                transaction.Inputs.Add(new TxIn()
                {
                    PrevOut = new OutPoint(new uint256(15), 1),
                    ScriptSig = new Script()
                });
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                ruleContext.BlockValidationContext.Block.Header.Time = (uint)1483747200;
                ruleContext.BlockValidationContext.Block.Transactions[1].Time = (uint)1483747201;

                Assert.True(BlockStake.IsProofOfStake(ruleContext.BlockValidationContext.Block));

                var rule = this.consensusRules.RegisterRule<PosCoinstakeRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BlockTimeBeforeTrx.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BlockTimeBeforeTrx.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_TransactionTimestampAfterBlockTimeStamp_ThrowsBlockTimeBeforeTrxConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    }
                };

                var transaction = new Transaction();
                transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                ruleContext.BlockValidationContext.Block.Header.Time = (uint)1483747200;
                ruleContext.BlockValidationContext.Block.Transactions[0].Time = (uint)1483747201;

                Assert.True(BlockStake.IsProofOfWork(ruleContext.BlockValidationContext.Block));

                var rule = this.consensusRules.RegisterRule<PosCoinstakeRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BlockTimeBeforeTrx.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BlockTimeBeforeTrx.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ValidBlock_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                }
            };

            var transaction = new Transaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            ruleContext.BlockValidationContext.Block.Header.Time = (uint)1483747200;
            ruleContext.BlockValidationContext.Block.Transactions[0].Time = (uint)1483747200;
            ruleContext.BlockValidationContext.Block.Transactions[1].Time = (uint)1483747200;

            Assert.True(BlockStake.IsProofOfStake(ruleContext.BlockValidationContext.Block));

            var rule = this.consensusRules.RegisterRule<PosCoinstakeRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_ValidBlock_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                }
            };

            var transaction = new Transaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);
            ruleContext.BlockValidationContext.Block.Header.Time = (uint)1483747200;
            ruleContext.BlockValidationContext.Block.Transactions[0].Time = (uint)1483747200;            

            Assert.True(BlockStake.IsProofOfWork(ruleContext.BlockValidationContext.Block));

            var rule = this.consensusRules.RegisterRule<PosCoinstakeRule>();

            await rule.RunAsync(ruleContext);
        }
    }
}
