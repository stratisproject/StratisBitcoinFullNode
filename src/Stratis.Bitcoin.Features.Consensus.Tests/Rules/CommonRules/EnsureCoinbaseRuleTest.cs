using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class EnsureCoinbaseRuleTest : TestConsensusRulesUnitTestBase
    {
        [Fact]
        public void RunAsync_BlockWithoutTransactions_ThrowsBadCoinbaseMissingConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<EnsureCoinbaseRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadCoinbaseMissing, exception.ConsensusError);
        }

        [Fact]
        public void RunAsync_FirstTransactionIsNotCoinbase_ThrowsBadCoinbaseMissingConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();

            var transaction = this.network.CreateTransaction();
            Assert.False(transaction.IsCoinBase);
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<EnsureCoinbaseRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadCoinbaseMissing, exception.ConsensusError);
        }

        [Fact]
        public void RunAsync_MultipleCoinsBaseTransactions_ThrowsBadMultipleCoinbaseConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();

            var transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), new Script()));
            transaction.Outputs.Add(new TxOut(new Money(3), (IDestination)null));

            Assert.True(transaction.IsCoinBase);
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<EnsureCoinbaseRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadMultipleCoinbase, exception.ConsensusError);
        }

        [Fact]
        public void RunAsync_SingleCoinBaseTransaction_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();

            var transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), new Script()));
            transaction.Outputs.Add(new TxOut(new Money(3), (IDestination)null));

            Assert.True(transaction.IsCoinBase);
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(new Transaction());

            this.consensusRules.RegisterRule<EnsureCoinbaseRule>().Run(this.ruleContext);
        }
    }
}