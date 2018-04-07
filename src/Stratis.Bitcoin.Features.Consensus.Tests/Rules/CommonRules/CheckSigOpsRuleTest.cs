using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CheckSigOpsRuleTest : TestConsensusRulesUnitTestBase
    {
        private PowConsensusOptions options;

        public CheckSigOpsRuleTest() : base()
        {
            this.options = this.network.Consensus.Option<PowConsensusOptions>();
        }

        [Fact]
        public async Task RunAsync_SingleTransactionInputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                    Consensus = this.network.Consensus
                };
                this.options.MaxBlockSigopsCost = 7;
                this.options.WitnessScaleFactor = 2;

                var transaction = new Transaction();
                var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
                transaction.Inputs.Add(new TxIn(new Script(op, op, op, op)));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadBlockSigOps.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadBlockSigOps.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionInputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                    Consensus = this.network.Consensus
                };
                this.options.MaxBlockSigopsCost = 7;
                this.options.WitnessScaleFactor = 2;

                var transaction = new Transaction();
                var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
                transaction.Inputs.Add(new TxIn(new Script(op, op)));
                transaction.Inputs.Add(new TxIn(new Script(op, op)));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadBlockSigOps.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadBlockSigOps.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionOutputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                    Consensus = this.network.Consensus
                };
                this.options.MaxBlockSigopsCost = 7;
                this.options.WitnessScaleFactor = 2;

                var transaction = new Transaction();
                var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
                transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op, op, op)));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadBlockSigOps.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadBlockSigOps.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionOutputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                    Consensus = this.network.Consensus
                };
                this.options.MaxBlockSigopsCost = 7;
                this.options.WitnessScaleFactor = 2;

                var transaction = new Transaction();
                var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
                transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
                transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadBlockSigOps.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadBlockSigOps.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_CombinedTransactionInputOutputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                    Consensus = this.network.Consensus
                };
                this.options.MaxBlockSigopsCost = 7;
                this.options.WitnessScaleFactor = 2;

                var transaction = new Transaction();
                var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
                transaction.Inputs.Add(new TxIn(new Script(op, op)));
                transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadBlockSigOps.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadBlockSigOps.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionInputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op, op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionInputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionOutputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op, op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionOutputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_CombinedTransactionInputOutputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionInputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op, op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionInputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionOutputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op, op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionOutputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_CombinedTransactionInputOutputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                Consensus = this.network.Consensus
            };
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckSigOpsRule>();

            await rule.RunAsync(ruleContext);
        }
    }
}
