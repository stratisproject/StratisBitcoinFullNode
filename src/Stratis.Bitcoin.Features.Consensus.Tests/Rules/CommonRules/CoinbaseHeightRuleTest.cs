using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CoinbaseHeightRuleTest : TestConsensusRulesUnitTestBase
    {
        public CoinbaseHeightRuleTest()
        {
        }

        [Fact]
        public async Task RunAsync_BestBlockAvailable_BadCoinBaseHeight_ThrowsBadCoinbaseHeightConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BestBlock = new ContextBlockInformation() { Height = 3 };

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new Script(Op.GetPushOp(3))));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CoinbaseHeightRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadCoinbaseHeight, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BestBlockUnAvailable_BadCoinBaseHeight_ThrowsBadCoinbaseHeightConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = new Block();

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new Script(Op.GetPushOp(3))));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CoinbaseHeightRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadCoinbaseHeight, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_CorrectCoinBaseHeight_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BestBlock = new ContextBlockInformation() { Height = 3 };

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new Script(Op.GetPushOp(4))));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CoinbaseHeightRule>().RunAsync(this.ruleContext);            
        }
    }
}
