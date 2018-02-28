using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class TxOutSmartContractExecRuleTest
    {
        public TxOutSmartContractExecRuleTest()
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;
        }

        [Fact]
        public async Task TxOutSmartContractExec_AllTransactions_ValidationSuccessAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            TxOutSmartContractExecRule rule = testContext.CreateRule<TxOutSmartContractExecRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block(new BlockHeader { HashPrevBlock = testContext.Chain.Tip.HashBlock });

            context.BlockValidationContext.Block.Transactions = new List<Transaction>
            {
                new Transaction
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_CREATECONTRACT))
                    }
                }
            };

            await rule.RunAsync(context);
        }

        [Fact]
        public async Task TxOutSmartContractExec_AllTransactions_ValidationFailAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            TxOutSmartContractExecRule rule = testContext.CreateRule<TxOutSmartContractExecRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block(new BlockHeader { HashPrevBlock = testContext.Chain.Tip.HashBlock });

            var transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_CALLCONTRACT)),
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_CREATECONTRACT))
                    }
                }
            };

            context.BlockValidationContext.Block.Transactions = transactions;           
            await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));

            transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_CALLCONTRACT)),
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_CALLCONTRACT))
                    }
                }
            };

            context.BlockValidationContext.Block.Transactions = transactions;
            await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));

            transactions = new List<Transaction>
            {
                new Transaction()
                {
                    Outputs =
                    {
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_CREATECONTRACT)),
                        new TxOut(Money.Zero, new Script(OpcodeType.OP_CREATECONTRACT))
                    }
                }
            };

            context.BlockValidationContext.Block.Transactions = transactions;
            await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }
    }
}