using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CheckPosTransactionRuleTest : TestPosConsensusRulesUnitTestBase
    {
        [Fact]
        public void CheckTransaction_TxOutsAreEmpty_TransactionIsCoinBase_DoesNotThrowException()
        {
            var transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            Assert.True(transaction.IsCoinBase);
            Assert.True(transaction.Outputs.All(t => t.IsEmpty));

            this.consensusRules.RegisterRule<CheckPosTransactionRule>().CheckTransaction(transaction);
        }

        [Fact]
        public void CheckTransaction_TxOutsAreEmpty_TransactionIsCoinStake_DoesNotThrowException()
        {
            var transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            Assert.True(transaction.IsCoinStake);
            Assert.True(transaction.Outputs.All(t => t.IsEmpty));

            this.consensusRules.RegisterRule<CheckPosTransactionRule>().CheckTransaction(transaction);
        }

        [Fact]
        public void CheckTransaction_TxOutsAreNotEmptyDoesNotThrowException()
        {
            var transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(new Money(150), (IDestination)null));

            Assert.False(transaction.Outputs[0].IsEmpty);

            this.consensusRules.RegisterRule<CheckPosTransactionRule>().CheckTransaction(transaction);
        }

        [Fact]
        public void CheckTransaction_TxOutsArePartiallyEmpty_TransactionNotCoinBaseOrCoinStake_ThrowsBadTransactionEmptyOutputConsensusErrorException()
        {
            var transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(new Money(150), (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            Assert.False(transaction.IsCoinBase);
            Assert.False(transaction.IsCoinStake);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPosTransactionRule>().CheckTransaction(transaction));

            Assert.Equal(ConsensusErrors.BadTransactionEmptyOutput, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BlockPassesCheckTransaction_DoesNotThrowExceptionAsync()
        {
            var transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            Assert.True(transaction.IsCoinBase);
            Assert.True(transaction.Outputs.All(t => t.IsEmpty));

            var ruleContext = new RuleContext()
            {
                ValidationContext = new ValidationContext()
                {
                    BlockToValidate = this.network.CreateBlock()
                }
            };

            ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);
            ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckPosTransactionRule>().RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_BlockFailsCheckTransaction_ThrowsBadTransactionEmptyOutputConsensusErrorExceptionAsync()
        {
            var validTransaction = this.network.CreateTransaction();
            validTransaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(),
                ScriptSig = new Script()
            });
            validTransaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            Assert.True(validTransaction.IsCoinBase);
            Assert.True(validTransaction.Outputs.All(t => t.IsEmpty));

            var invalidTransaction = this.network.CreateTransaction();
            invalidTransaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            invalidTransaction.Outputs.Add(new TxOut(new Money(150), (IDestination)null));
            invalidTransaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            Assert.False(invalidTransaction.IsCoinBase);
            Assert.False(invalidTransaction.IsCoinStake);

            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(validTransaction);
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(invalidTransaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPosTransactionRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadTransactionEmptyOutput, exception.ConsensusError);
        }
    }
}