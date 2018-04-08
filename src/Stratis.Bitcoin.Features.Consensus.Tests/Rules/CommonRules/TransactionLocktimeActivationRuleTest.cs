using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class TransactionLocktimeActivationRuleTest : TestConsensusRulesUnitTestBase
    {
        public TransactionLocktimeActivationRuleTest() : base()
        {

        }

        [Fact]
        public async Task RunAsync_DoesNotHaveBIP113Flag_TransactionNotFinal_ThrowsBadTransactionNonFinalConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Flags = new Base.Deployments.DeploymentFlags(),
                    BestBlock = new ContextBlockInformation()
                    {
                        Height = 12,
                    },
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                };

                var transaction = new Transaction();
                transaction.LockTime = new DateTimeOffset(new DateTime(2018, 1, 3, 0, 0, 0, DateTimeKind.Utc));
                transaction.Inputs.Add(new TxIn() { Sequence = 15 });
                ruleContext.BlockValidationContext.Block.AddTransaction(transaction);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                var rule = this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadTransactionNonFinal.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionNonFinal.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_HasBIP113Flag_TransactionNotFinal_ThrowsBadTransactionNonFinalConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Flags = new Base.Deployments.DeploymentFlags() { LockTimeFlags = Transaction.LockTimeFlags.MedianTimePast },
                    BestBlock = new ContextBlockInformation()
                    {
                        Height = 12,
                        MedianTimePast = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                    },
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                };

                var transaction = new Transaction();
                transaction.LockTime = new DateTimeOffset(new DateTime(2018, 1, 3, 0, 0, 0, DateTimeKind.Utc));
                transaction.Inputs.Add(new TxIn() { Sequence = 15 });
                ruleContext.BlockValidationContext.Block.AddTransaction(transaction);

                var rule = this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadTransactionNonFinal.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadTransactionNonFinal.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_DoesNotHaveBIP113Flag_TransactionFinal_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                Flags = new Base.Deployments.DeploymentFlags(),
                BestBlock = new ContextBlockInformation()
                {
                    Height = 12,
                },
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
            };

            var transaction = new Transaction();
            ruleContext.BlockValidationContext.Block.AddTransaction(transaction);
            ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var rule = this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>();
            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_HasBIP113Flag_TransactionFinal_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                Flags = new Base.Deployments.DeploymentFlags() { LockTimeFlags = Transaction.LockTimeFlags.MedianTimePast },
                BestBlock = new ContextBlockInformation()
                {
                    Height = 12,
                    MedianTimePast = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                },
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
            };

            var transaction = new Transaction();
            ruleContext.BlockValidationContext.Block.AddTransaction(transaction);

            var rule = this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>();
            await rule.RunAsync(ruleContext);
        }
    }
}
