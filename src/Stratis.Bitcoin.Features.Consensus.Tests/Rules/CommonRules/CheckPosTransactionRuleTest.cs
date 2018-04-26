using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CheckPosTransactionRuleTest : TestPosConsensusRulesUnitTestBase
    {
        public CheckPosTransactionRuleTest()
        {
        }

        [Fact]
        public void CheckTransaction_TxOutsAreEmpty_TransactionIsCoinBase_DoesNotThrowException()
        {
            var transaction = new Transaction();
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
            var transaction = new Transaction();
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
            var transaction = new Transaction();
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
            var transaction = new Transaction();
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
            var transaction = new Transaction();
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
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new Block()
                }
            };

            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckPosTransactionRule>().RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_BlockFailsCheckTransaction_ThrowsBadTransactionEmptyOutputConsensusErrorExceptionAsync()
        {
            var validTransaction = new Transaction();
            validTransaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(),
                ScriptSig = new Script()
            });
            validTransaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            Assert.True(validTransaction.IsCoinBase);
            Assert.True(validTransaction.Outputs.All(t => t.IsEmpty));

            var invalidTransaction = new Transaction();
            invalidTransaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            invalidTransaction.Outputs.Add(new TxOut(new Money(150), (IDestination)null));
            invalidTransaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            Assert.False(invalidTransaction.IsCoinBase);
            Assert.False(invalidTransaction.IsCoinStake);

            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(validTransaction);
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(invalidTransaction);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckPosTransactionRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadTransactionEmptyOutput, exception.ConsensusError);
        }
    }
}
