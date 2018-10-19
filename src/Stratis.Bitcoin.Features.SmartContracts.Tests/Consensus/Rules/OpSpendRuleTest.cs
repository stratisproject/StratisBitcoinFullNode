using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.Core;
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
                    Outputs =
                    {
                        new TxOut(new Money(1000), new Script(new [] { (byte) ScOpcodeType.OP_SPEND}))
                    }
                }
            };

            await rule.RunAsync(context);
        }

        [Fact]
        public void OpSpend_PreviousTransactionNone_FailureAsync()
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
                        new TxOut(new Money(1000), new Script(new [] { (byte) ScOpcodeType.OP_SPEND}))
                    }
                }
            };

            Task<ConsensusErrorException> error = Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }

        [Fact]
        public void OpSpend_PreviousTransactionOther_FailureAsync()
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
                    Outputs =
                    {
                        new TxOut(new Money(1000), new Script(new [] { (byte) ScOpcodeType.OP_SPEND}))
                    }
                }
            };

            Task<ConsensusErrorException> error = Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }
    }
}