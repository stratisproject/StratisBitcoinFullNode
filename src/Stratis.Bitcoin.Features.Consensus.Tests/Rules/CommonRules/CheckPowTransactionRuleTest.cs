using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CheckPowTransactionRuleTest : TestConsensusRulesUnitTestBase
    {
        private PowConsensusOptions options;

        public CheckPowTransactionRuleTest() : base()
        {
            this.options = this.network.Consensus.Option<PowConsensusOptions>();
        }

        [Fact]
        public void CheckTransaction_NoInputs_ThrowsBadTransactionNoInputConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {                
                var transaction = new Transaction();
                transaction.Inputs.Clear();
                transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
                transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadTransactionNoInput.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionNoInput.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_NoOutputs_ThrowsBadTransactionNoOutputConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {
                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
                transaction.Outputs.Clear();

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadTransactionNoOutput.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionNoOutput.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_TransactionOverSized_ThrowsBadTransactionOversizeConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {
                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn());
                transaction = GenerateTransactionWithWeight(transaction, this.options.MaxBlockBaseSize + 1, NetworkOptions.TemporaryOptions & ~NetworkOptions.Witness);

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadTransactionOversize.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionOversize.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_TxOutNegativeValue_ThrowsBadTransactionNegativeOutputConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {
                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn());
                transaction.Outputs.Add(new TxOut(new Money(-1), (IDestination)null));

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadTransactionNegativeOutput.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionNegativeOutput.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_TxOutValueAboveMaxMoney_ThrowsBadTransactionTooLargeOutputConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {
                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn());
                transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney + 1), (IDestination)null));

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadTransactionTooLargeOutput.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionTooLargeOutput.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_TotalTxOutValueAboveMaxMoney_ThrowsBadTransactionTooLargeTotalOutputConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {
                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn());
                transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
                transaction.Outputs.Add(new TxOut(new Money((this.options.MaxMoney / 2) + 1), (IDestination)null));

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadTransactionTooLargeTotalOutput.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionTooLargeTotalOutput.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_DuplicateTxInOutPoint_ThrowsBadTransactionDuplicateInputsConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {
                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn(new OutPoint(new uint256(1500), 15)));
                transaction.Inputs.Add(new TxIn(new OutPoint(new uint256(1500), 15)));
                transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadTransactionDuplicateInputs.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionDuplicateInputs.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_CoinBaseTransactionWithBadCoinBaseSizeSmaller_ThrowsBadCoinbaseSizeConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {
                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 1).Select(c => (byte)c).ToArray())));
                transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
                Assert.True(transaction.IsCoinBase);

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadCoinbaseSize.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadCoinbaseSize.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_CoinBaseTransactionWithBadCoinBaseSizeLarger_ThrowsBadCoinbaseSizeConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {
                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 101).Select(c => (byte)c).ToArray())));
                transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
                Assert.True(transaction.IsCoinBase);

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadCoinbaseSize.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadCoinbaseSize.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_NonCoinBaseTransaction_WithTxInNullPrevout_ThrowsBadTransactionNullPrevoutConsensusErrorsException()
        {
            var exception = Assert.Throws<ConsensusErrorException>(() =>
            {
                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn(new OutPoint(new uint256(15), 1)));
                transaction.Inputs.Add(new TxIn(new OutPoint()));
                transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
                Assert.False(transaction.IsCoinBase);

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
                rule.CheckTransaction(this.options, transaction);
            });

            Assert.Equal(ConsensusErrors.BadTransactionNullPrevout.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionNullPrevout.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void CheckTransaction_ZeroMoneyTxOut_DoesNotThrowException()
        {

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(0), (IDestination)null));

            var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
            rule.CheckTransaction(this.options, transaction);
        }

        [Fact]
        public void CheckTransaction_MaxMoneyTxOut_DoesNotThrowException()
        {

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney), (IDestination)null));

            var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
            rule.CheckTransaction(this.options, transaction);
        }

        [Fact]
        public void CheckTransaction_MaxMoneyTxOutTotal_DoesNotThrowException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
            transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));

            var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
            rule.CheckTransaction(this.options, transaction);
        }

        [Fact]
        public void CheckTransaction_CoinBaseTransactionWithMinimalScriptSize_DoesNotThrowException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 2).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
            Assert.True(transaction.IsCoinBase);

            var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
            rule.CheckTransaction(this.options, transaction);
        }

        [Fact]
        public void CheckTransaction_CoinBaseTransactionWithMaximalScriptSize_DoesNotThrowException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 100).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
            Assert.True(transaction.IsCoinBase);

            var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();
            rule.CheckTransaction(this.options, transaction);
        }

        [Fact]
        public async Task RunAsync_BlockPassesCheckTransaction_DoesNotThrowExceptionAsync()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
            transaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));            

            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new Block()
                },
                Consensus = this.network.Consensus
            };

            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_BlockFailsCheckTransaction_ThrowsBadTransactionEmptyOutputConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var validTransaction = new Transaction();
                validTransaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
                validTransaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));
                validTransaction.Outputs.Add(new TxOut(new Money(this.options.MaxMoney / 2), (IDestination)null));

                var invalidTransaction = new Transaction();

                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new Block()
                    },
                    Consensus = this.network.Consensus
                };                

                ruleContext.BlockValidationContext.Block.Transactions.Add(validTransaction);
                ruleContext.BlockValidationContext.Block.Transactions.Add(invalidTransaction);

                var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadTransactionNoInput.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionNoInput.Message, exception.ConsensusError.Message);
        }

        private Transaction GenerateTransactionWithWeight(Transaction transaction, int weight, NetworkOptions options)
        {
            transaction.Outputs.Add(new TxOut(new Money(10000000000), new Script()));

            int transactionWeight = this.CalculateBlockWeight(transaction, options);

            var requiredScriptWeight = weight - transactionWeight - 4;
            transaction.Outputs.RemoveAt(transaction.Outputs.Count - 1);
            // generate nonsense script with required bytes to reach required weight.
            var script = Script.FromBytesUnsafe(new string('A', requiredScriptWeight).Select(c => (byte)c).ToArray());
            transaction.Outputs.Add(new TxOut(new Money(10000000000), script));

            transactionWeight = this.CalculateBlockWeight(transaction, options);

            if (transactionWeight == weight)
            {
                return transaction;
            }

            return null;
        }

        private int CalculateBlockWeight(Transaction transaction, NetworkOptions options)
        {
            using (var stream = new MemoryStream())
            {
                var bms = new BitcoinStream(stream, true);
                bms.TransactionOptions = options;
                transaction.ReadWrite(bms);
                return (int)bms.Counter.WrittenBytes;
            }
        }
    }
}
