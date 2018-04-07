using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class EnsureCoinbaseRuleTest : TestConsensusRulesUnitTestBase
    {
        public EnsureCoinbaseRuleTest() : base()
        {
        }

        [Fact]
        public async Task RunAsync_BlockWithoutTransactions_ThrowsBadCoinbaseMissingConsensusErrorExceptionAsync()
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

                var rule = this.consensusRules.RegisterRule<EnsureCoinbaseRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadCoinbaseMissing.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadCoinbaseMissing.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_FirstTransactionIsNotCoinbase_ThrowsBadCoinbaseMissingConsensusErrorExceptionAsync()
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
                Assert.False(transaction.IsCoinBase);
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                var rule = this.consensusRules.RegisterRule<EnsureCoinbaseRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadCoinbaseMissing.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadCoinbaseMissing.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_MultipleCoinsBaseTransactions_ThrowsBadMultipleCoinbaseConsensusErrorExceptionAsync()
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
                transaction.Inputs.Add(new TxIn(new OutPoint(), new Script()));
                transaction.Outputs.Add(new TxOut(new Money(3), (IDestination)null));

                Assert.True(transaction.IsCoinBase);
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                var rule = this.consensusRules.RegisterRule<EnsureCoinbaseRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadMultipleCoinbase.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadMultipleCoinbase.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_SingleCoinBaseTransaction_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                }
            };

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(), new Script()));
            transaction.Outputs.Add(new TxOut(new Money(3), (IDestination)null));

            Assert.True(transaction.IsCoinBase);
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);
            ruleContext.BlockValidationContext.Block.Transactions.Add(new Transaction());

            var rule = this.consensusRules.RegisterRule<EnsureCoinbaseRule>();

            await rule.RunAsync(ruleContext);
        }
    }
}
