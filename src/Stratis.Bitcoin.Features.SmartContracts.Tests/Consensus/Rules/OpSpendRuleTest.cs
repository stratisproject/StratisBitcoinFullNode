using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Xunit;
using Xunit.Sdk;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class OpSpendRuleTest
    {
        public OpSpendRuleTest()
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;
        }

        [Fact]
        public async Task OpSpend_PreviousTransactionOpCall_SuccessAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            OpSpendRule rule = testContext.CreateRule<OpSpendRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block(new BlockHeader { HashPrevBlock = testContext.Chain.Tip.HashBlock });

            context.BlockValidationContext.Block.Transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_CALLCONTRACT))
                    }
                },
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(new Money(1000), new Script(OpcodeType.OP_SPEND))
                    }
                }
            };

            await rule.RunAsync(context);
        }

        [Fact]
        public async Task OpSpend_PreviousTransactionNone_FailureAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            OpSpendRule rule = testContext.CreateRule<OpSpendRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block(new BlockHeader { HashPrevBlock = testContext.Chain.Tip.HashBlock });

            context.BlockValidationContext.Block.Transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(new Money(1000), new Script(OpcodeType.OP_SPEND))
                    }
                }
            };

            var error = Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }

        [Fact]
        public async Task OpSpend_PreviousTransactionOther_FailureAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            OpSpendRule rule = testContext.CreateRule<OpSpendRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block(new BlockHeader { HashPrevBlock = testContext.Chain.Tip.HashBlock });

            context.BlockValidationContext.Block.Transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_NOP))
                    }
                },
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_NOP))
                    }
                },
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_NOP))
                    }
                },
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(new Money(1000), new Script(OpcodeType.OP_SPEND))
                    }
                }
            };

            var error = Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }

    }
}
