using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class UpdateCoinViewRuleTests
    {
        private readonly RuleContext ruleContext;
        private readonly List<Transaction> transactions;
        private Exception caughtExecption;

        public UpdateCoinViewRuleTests()
        {
            this.ruleContext = new RuleContext { };
            this.ruleContext.BlockValidationContext = new BlockValidationContext();
            this.ruleContext.Set = new UnspentOutputSet();
            this.ruleContext.Set.SetCoins(new UnspentOutputs[0]);
            this.transactions = new List<Transaction>();
            this.ruleContext.BlockValidationContext.Block = new Block()
            {
                Transactions = this.transactions
            };
        }

        [Fact]
        public void RunAsync_TransactionThatIsNotCoinBaseButStillHasUnspentOutputsWithoutInput_ThrowsBadTransactionMissingInput()
        {
            this.GivenACoinbaseTransaction();
            this.AndATransactionWithNoUnspentOutputsAsInput();
            this.WhenExecutingTheRule();
            this.ThenBadTransactionMissingInputIsThrown();
        }

        private void GivenACoinbaseTransaction()
        {
            var transactionWithCoinbase = new Transaction();
            var txIn = new TxIn { PrevOut = new OutPoint() };
            transactionWithCoinbase.AddInput(txIn);
            transactionWithCoinbase.AddOutput(new TxOut());
            this.transactions.Add(transactionWithCoinbase);
        }

        private void AndATransactionWithNoUnspentOutputsAsInput()
        {
            this.transactions.Add(new Transaction { Inputs = { new TxIn() } });
        }

        private void WhenExecutingTheRule()
        {
            try
            {
                var rule = new UpdateCoinViewRule { Logger = new Mock<ILogger>().Object };
                rule.RunAsync(this.ruleContext).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                this.caughtExecption = e;
            }
        }

        private void ThenBadTransactionMissingInputIsThrown()
        {
            this.caughtExecption.Should().BeOfType<ConsensusErrorException>();
            var consensusErrorException = (ConsensusErrorException)this.caughtExecption;
            consensusErrorException.ConsensusError.Should().Be(ConsensusErrors.BadTransactionMissingInput);
        }
    }
}