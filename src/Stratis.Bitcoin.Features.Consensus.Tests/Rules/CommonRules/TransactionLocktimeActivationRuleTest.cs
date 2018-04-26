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
        public TransactionLocktimeActivationRuleTest()
        {
            this.ruleContext.BlockValidationContext.Block = new Block();
        }

        [Fact]
        public async Task RunAsync_DoesNotHaveBIP113Flag_TransactionNotFinal_ThrowsBadTransactionNonFinalConsensusErrorExceptionAsync()
        {
            this.ruleContext.Flags = new Base.Deployments.DeploymentFlags();
            this.ruleContext.BestBlock = new ContextBlockInformation() { Height = 12, };

            var transaction = new Transaction();
            transaction.LockTime = new DateTimeOffset(new DateTime(2018, 1, 3, 0, 0, 0, DateTimeKind.Utc));
            transaction.Inputs.Add(new TxIn() { Sequence = 15 });
            this.ruleContext.BlockValidationContext.Block.AddTransaction(transaction);
            this.ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadTransactionNonFinal, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_HasBIP113Flag_TransactionNotFinal_ThrowsBadTransactionNonFinalConsensusErrorExceptionAsync()
        {
            this.ruleContext.Flags = new Base.Deployments.DeploymentFlags() { LockTimeFlags = Transaction.LockTimeFlags.MedianTimePast };
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                Height = 12,
                MedianTimePast = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            };

            var transaction = new Transaction();
            transaction.LockTime = new DateTimeOffset(new DateTime(2018, 1, 3, 0, 0, 0, DateTimeKind.Utc));
            transaction.Inputs.Add(new TxIn() { Sequence = 15 });
            this.ruleContext.BlockValidationContext.Block.AddTransaction(transaction);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadTransactionNonFinal, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_DoesNotHaveBIP113Flag_TransactionFinal_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.Flags = new Base.Deployments.DeploymentFlags();
            this.ruleContext.BestBlock = new ContextBlockInformation() { Height = 12, };

            var transaction = new Transaction();
            this.ruleContext.BlockValidationContext.Block.AddTransaction(transaction);
            this.ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            await this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_HasBIP113Flag_TransactionFinal_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.Flags = new Base.Deployments.DeploymentFlags() { LockTimeFlags = Transaction.LockTimeFlags.MedianTimePast };
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                Height = 12,
                MedianTimePast = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            };

            var transaction = new Transaction();
            this.ruleContext.BlockValidationContext.Block.AddTransaction(transaction);

            await this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>().RunAsync(this.ruleContext);
        }
    }
}
