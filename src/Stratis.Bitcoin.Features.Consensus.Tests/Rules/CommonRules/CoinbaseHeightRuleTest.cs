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
        public CoinbaseHeightRuleTest() : base()
        {
        }

        [Fact]
        public async Task RunAsync_BestBlockAvailable_BadCoinBaseHeight_ThrowsBadCoinbaseHeightConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                    BestBlock = new ContextBlockInformation() { Height = 3 }
                };

                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn(new Script(Op.GetPushOp(3))));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                var rule = this.consensusRules.RegisterRule<CoinbaseHeightRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadCoinbaseHeight.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadCoinbaseHeight.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BestBlockUnAvailable_BadCoinBaseHeight_ThrowsBadCoinbaseHeightConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                };

                var transaction = new Transaction();
                transaction.Inputs.Add(new TxIn(new Script(Op.GetPushOp(3))));
                ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

                var rule = this.consensusRules.RegisterRule<CoinbaseHeightRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadCoinbaseHeight.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadCoinbaseHeight.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_CorrectCoinBaseHeight_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
                BestBlock = new ContextBlockInformation() { Height = 3 }
            };

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new Script(Op.GetPushOp(4))));
            ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            var rule = this.consensusRules.RegisterRule<CoinbaseHeightRule>();

            await rule.RunAsync(ruleContext);
        }
    }
}
