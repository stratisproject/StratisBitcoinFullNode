using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class OpSpendRuleTest
    {
        private readonly Network network;

        public OpSpendRuleTest()
        {
            this.network = new SmartContractsRegTest();
        }

        [Fact]
        public async Task OpSpend_PreviousTransactionOpCall_SuccessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            OpSpendRule rule = testContext.CreateRule<OpSpendRule>();

            var context = new RuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());

            context.ValidationContext.BlockToValidate = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.BlockToValidate.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
            context.ValidationContext.BlockToValidate.Transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(new [] { (byte) ScOpcodeType.OP_CALLCONTRACT }))
                    }
                },
                new Transaction()
                {
                    Inputs =
                    {
                        new TxIn(new Script(new [] { (byte) ScOpcodeType.OP_SPEND}))
                    }
                }
            };

            await rule.RunAsync(context);
        }

        [Fact]
        public async Task OpSpend_PreviousTransactionNone_FailureAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            OpSpendRule rule = testContext.CreateRule<OpSpendRule>();

            var context = new RuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.BlockToValidate.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
            context.ValidationContext.BlockToValidate.Transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Inputs =
                    {
                        new TxIn(new Script(new [] { (byte) ScOpcodeType.OP_SPEND}))
                    }
                }
            };

            await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }

        [Fact]
        public async Task OpSpend_PreviousTransactionOther_FailureAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            OpSpendRule rule = testContext.CreateRule<OpSpendRule>();

            var context = new RuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.BlockToValidate.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
            context.ValidationContext.BlockToValidate.Transactions = new List<Transaction>
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
                    Inputs =
                    {
                        new TxIn(new Script(new [] { (byte) ScOpcodeType.OP_SPEND}))
                    }
                }
            };

            await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }
    }
}