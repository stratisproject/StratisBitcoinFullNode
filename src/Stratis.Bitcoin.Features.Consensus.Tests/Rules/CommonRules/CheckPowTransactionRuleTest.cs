using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CheckPowTransactionRuleTest : TestConsensusRulesUnitTestBase
    {
        private ConsensusOptions options;

        private IConsensus consensus;

        public CheckPowTransactionRuleTest()
        {
            this.consensus = this.network.Consensus;
            this.options = this.consensus.Options;
        }

        [Fact]
        public void CheckTransaction_NoInputs_ThrowsBadTransactionNoInputConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Clear();
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadTransactionNoInput, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_NoOutputs_ThrowsBadTransactionNoOutputConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Clear();

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadTransactionNoOutput, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_TransactionOverSized_ThrowsBadTransactionOversizeConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn());
            transaction = this.GenerateTransactionWithWeight(transaction,(int)this.options.MaxBlockBaseSize + 1, TransactionOptions.None);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadTransactionOversize, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_TxOutNegativeValue_ThrowsBadTransactionNegativeOutputConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn());
            transaction.Outputs.Add(new TxOut(new Money(-1), (IDestination)null));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadTransactionNegativeOutput, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_TxOutValueAboveMaxMoney_ThrowsBadTransactionTooLargeOutputConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn());
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney + 1), (IDestination)null));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadTransactionTooLargeOutput, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_TotalTxOutValueAboveMaxMoney_ThrowsBadTransactionTooLargeTotalOutputConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn());
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            transaction.Outputs.Add(new TxOut(new Money((this.consensus.MaxMoney / 2) + 1), (IDestination)null));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadTransactionTooLargeTotalOutput, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_DuplicateTxInOutPoint_ThrowsBadTransactionDuplicateInputsConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(new uint256(1500), 15)));
            transaction.Inputs.Add(new TxIn(new OutPoint(new uint256(1500), 15)));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadTransactionDuplicateInputs, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_CoinBaseTransactionWithBadCoinBaseSizeSmaller_ThrowsBadCoinbaseSizeConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 1).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            Assert.True(transaction.IsCoinBase);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadCoinbaseSize, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_CoinBaseTransactionWithBadCoinBaseSizeLarger_ThrowsBadCoinbaseSizeConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 101).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            Assert.True(transaction.IsCoinBase);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadCoinbaseSize, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_NonCoinBaseTransaction_WithTxInNullPrevout_ThrowsBadTransactionNullPrevoutConsensusErrorsException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(new uint256(15), 1)));
            transaction.Inputs.Add(new TxIn(new OutPoint()));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            Assert.False(transaction.IsCoinBase);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction));

            Assert.Equal(ConsensusErrors.BadTransactionNullPrevout, exception.ConsensusError);
        }

        [Fact]
        public void CheckTransaction_ZeroMoneyTxOut_DoesNotThrowException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(0), (IDestination)null));

            this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction);
        }

        [Fact]
        public void CheckTransaction_MaxMoneyTxOut_DoesNotThrowException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney), (IDestination)null));

            this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction);
        }

        [Fact]
        public void CheckTransaction_MaxMoneyTxOutTotal_DoesNotThrowException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));

            this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction);
        }

        [Fact]
        public void CheckTransaction_CoinBaseTransactionWithMinimalScriptSize_DoesNotThrowException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 2).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            Assert.True(transaction.IsCoinBase);

            this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction);
        }

        [Fact]
        public void CheckTransaction_CoinBaseTransactionWithMaximalScriptSize_DoesNotThrowException()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 100).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            Assert.True(transaction.IsCoinBase);

            this.consensusRules.RegisterRule<CheckPowTransactionRule>().CheckTransaction(this.network, this.options, transaction);
        }

        [Fact]
        public async Task RunAsync_BlockPassesCheckTransaction_DoesNotThrowExceptionAsync()
        {
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            transaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));

            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CheckPowTransactionRule>();

            await rule.RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_BlockFailsCheckTransaction_ThrowsBadTransactionEmptyOutputConsensusErrorExceptionAsync()
        {
            var validTransaction = new Transaction();
            validTransaction.Inputs.Add(new TxIn(new OutPoint(), Script.FromBytesUnsafe(new string('A', 50).Select(c => (byte)c).ToArray())));
            validTransaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));
            validTransaction.Outputs.Add(new TxOut(new Money(this.consensus.MaxMoney / 2), (IDestination)null));

            var invalidTransaction = new Transaction();

            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(validTransaction);
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(invalidTransaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPowTransactionRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadTransactionNoInput, exception.ConsensusError);
        }

        private Transaction GenerateTransactionWithWeight(Transaction transaction, int weight, TransactionOptions options)
        {
            transaction.Outputs.Add(new TxOut(new Money(10000000000), new Script()));

            int transactionWeight = this.CalculateBlockWeight(transaction, options);

            int requiredScriptWeight = weight - transactionWeight - 4;
            transaction.Outputs.RemoveAt(transaction.Outputs.Count - 1);
            // generate nonsense script with required bytes to reach required weight.
            Script script = Script.FromBytesUnsafe(new string('A', requiredScriptWeight).Select(c => (byte)c).ToArray());
            transaction.Outputs.Add(new TxOut(new Money(10000000000), script));

            transactionWeight = this.CalculateBlockWeight(transaction, options);

            if (transactionWeight == weight)
            {
                return transaction;
            }

            return null;
        }

        private int CalculateBlockWeight(Transaction transaction, TransactionOptions options)
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
