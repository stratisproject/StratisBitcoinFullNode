using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class OpSpendRuleTest
    {
        [Fact]
        public async Task OpSpend_PreviousTransactionOpCall_SuccessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.SmartContractsRegTest);
            OpSpendRule rule = testContext.CreateRule<OpSpendRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.SmartContractsRegTest.Consensus, testContext.Chain.Tip);

            context.BlockValidationContext.Block = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.BlockValidationContext.Block.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
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
        public void OpSpend_PreviousTransactionNone_FailureAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.SmartContractsRegTest);
            OpSpendRule rule = testContext.CreateRule<OpSpendRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.SmartContractsRegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.BlockValidationContext.Block.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
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

            Task<ConsensusErrorException> error = Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }

        [Fact]
        public void OpSpend_PreviousTransactionOther_FailureAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.SmartContractsRegTest);
            OpSpendRule rule = testContext.CreateRule<OpSpendRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.SmartContractsRegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.BlockValidationContext.Block.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
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

            Task<ConsensusErrorException> error = Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }
    }
}