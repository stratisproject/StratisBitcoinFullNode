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
    public class TxOutSmartContractExecRuleTest
    {
        private Network network;

        public TxOutSmartContractExecRuleTest()
        {
            this.network = new SmartContractsRegTest();
        }

        [Fact]
        public async Task TxOutSmartContractExec_AllTransactions_ValidationSuccessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            TxOutSmartContractExecRule rule = testContext.CreateRule<TxOutSmartContractExecRule>();

            var context = new RuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.BlockToValidate.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
            context.ValidationContext.BlockToValidate.Transactions = new List<Transaction>
            {
                new Transaction
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(new [] { (byte)ScOpcodeType.OP_CREATECONTRACT }))
                    }
                }
            };

            await rule.RunAsync(context);
        }

        [Fact]
        public async Task TxOutSmartContractExec_AllTransactions_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            TxOutSmartContractExecRule rule = testContext.CreateRule<TxOutSmartContractExecRule>();

            var context = new RuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.BlockToValidate.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;

            var transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(new [] { (byte)ScOpcodeType.OP_CALLCONTRACT })),
                        new TxOut(Money.Zero, new Script(new [] { (byte)ScOpcodeType.OP_CREATECONTRACT }))
                    }
                }
            };

            context.ValidationContext.BlockToValidate.Transactions = transactions;
            await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));

            transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(new [] { (byte)ScOpcodeType.OP_CALLCONTRACT })),
                        new TxOut(Money.Zero, new Script(new [] { (byte)ScOpcodeType.OP_CALLCONTRACT }))
                    }
                }
            };

            context.ValidationContext.BlockToValidate.Transactions = transactions;
            await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));

            transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(new [] { (byte)ScOpcodeType.OP_CREATECONTRACT })),
                        new TxOut(Money.Zero, new Script(new [] { (byte)ScOpcodeType.OP_CREATECONTRACT }))
                    }
                }
            };

            context.ValidationContext.BlockToValidate.Transactions = transactions;
            await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }

        [Fact]
        public async Task TxOutSmartContractExec_CoinbaseWithSmartContracts_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            TxOutSmartContractExecRule rule = testContext.CreateRule<TxOutSmartContractExecRule>();

            var context = new RuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.ValidationContext.BlockToValidate.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;

            var transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(new [] { (byte)ScOpcodeType.OP_CALLCONTRACT }))
                    },
                    Inputs =
                    {
                        TxIn.CreateCoinbase(0)
                    }
                }
            };

            context.ValidationContext.BlockToValidate.Transactions = transactions;
            await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));

            transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script()),
                    },
                    Inputs =
                    {
                        TxIn.CreateCoinbase(0)
                    }
                }
            };

            context.ValidationContext.BlockToValidate.Transactions = transactions;
            await rule.RunAsync(context);
        }
    }
}