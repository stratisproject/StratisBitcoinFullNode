using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class EnsureCoinbaseRuleTest : TestConsensusRulesUnitTestBase
    {
        public EnsureCoinbaseRuleTest()
        {
        }

        [Fact]
        public async Task RunAsync_BlockWithoutTransactions_ThrowsBadCoinbaseMissingConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext.Block = new Block();

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<EnsureCoinbaseRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadCoinbaseMissing, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_FirstTransactionIsNotCoinbase_ThrowsBadCoinbaseMissingConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext.Block = new Block();

            var transaction = new Transaction();
            Assert.False(transaction.IsCoinBase);
            this.ruleContext.ValidationContext.Block.Transactions.Add(transaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<EnsureCoinbaseRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadCoinbaseMissing, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_MultipleCoinsBaseTransactions_ThrowsBadMultipleCoinbaseConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext.Block = new Block();

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), new Script()));
            transaction.Outputs.Add(new TxOut(new Money(3), (IDestination)null));

            Assert.True(transaction.IsCoinBase);
            this.ruleContext.ValidationContext.Block.Transactions.Add(transaction);
            this.ruleContext.ValidationContext.Block.Transactions.Add(transaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<EnsureCoinbaseRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadMultipleCoinbase, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_SingleCoinBaseTransaction_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.ValidationContext.Block = new Block();

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), new Script()));
            transaction.Outputs.Add(new TxOut(new Money(3), (IDestination)null));

            Assert.True(transaction.IsCoinBase);
            this.ruleContext.ValidationContext.Block.Transactions.Add(transaction);
            this.ruleContext.ValidationContext.Block.Transactions.Add(new Transaction());

            await this.consensusRules.RegisterRule<EnsureCoinbaseRule>().RunAsync(ruleContext);
        }
    }
}
