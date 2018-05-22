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
        private Network network;

        public TxOutSmartContractExecRuleTest()
        {
            this.network = Network.SmartContractsRegTest;
        }

        [Fact]
        public async Task TxOutSmartContractExec_AllTransactions_ValidationSuccessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            TxOutSmartContractExecRule rule = testContext.CreateRule<TxOutSmartContractExecRule>();

            var context = new RuleContext(new BlockValidationContext(), testContext.Network.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.BlockValidationContext.Block.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;
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
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            TxOutSmartContractExecRule rule = testContext.CreateRule<TxOutSmartContractExecRule>();

            var context = new RuleContext(new BlockValidationContext(), testContext.Network.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = testContext.Network.Consensus.ConsensusFactory.CreateBlock();
            context.BlockValidationContext.Block.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;

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