using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Xunit;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class GasBudgetRuleTests
    {
        public GasBudgetRuleTests()
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;
        }

        private UnspentOutputSet GetMockOutputSet()
        {
            var fakeTxOut = new TxOut();
            fakeTxOut.Value = 100_000_000;

            var unspentOutputMock = new Mock<UnspentOutputSet>();
            unspentOutputMock.Setup(x => x.AccessCoins(It.IsAny<uint256>()))
                .Returns(new UnspentOutputs()
                {
                    Outputs = new TxOut[]
                    {
                        fakeTxOut
                    }
                });

            return unspentOutputMock.Object;
        }

        [Fact]
        public async Task GasBudgetRule_SuccessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            GasBudgetRule rule = testContext.CreateRule<GasBudgetRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus,
                testContext.Chain.Tip)
            {
                Set = GetMockOutputSet()
            };

            context.BlockValidationContext.Block = new Block();

            var gasPriceSatoshis = 20;
            var gasLimit = 4_000_000;
            var gasBudgetSatoshis = gasPriceSatoshis * gasLimit;
            var relayFeeSatoshis = 10000;

            var totalSuppliedSatoshis = gasBudgetSatoshis + relayFeeSatoshis;

            var carrier = SmartContractCarrier.CallContract(1, 0, "TestMethod", (ulong)gasPriceSatoshis, (Gas)gasLimit);
            var serialized = carrier.Serialize();

            Transaction funding = new Transaction
            {
                Outputs =
                {
                    new TxOut(totalSuppliedSatoshis, new Script())
                }
            };

            var transactionBuilder = new TransactionBuilder();
            transactionBuilder.AddCoins(funding);
            transactionBuilder.SendFees(relayFeeSatoshis + gasBudgetSatoshis);
            transactionBuilder.Send(new Script(serialized), 0);

            Transaction transaction = transactionBuilder.BuildTransaction(false);

            context.BlockValidationContext.Block.Transactions = new List<Transaction>
            {
               transaction
            };

            await rule.RunAsync(context);
        }

        [Fact]
        public async Task GasBudgetRule_MultipleOutputs_SuccessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            GasBudgetRule rule = testContext.CreateRule<GasBudgetRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip)
            {
                Set = GetMockOutputSet()
            };

            context.BlockValidationContext.Block = new Block();

            var gasPriceSatoshis = 20;
            var gasLimit = 4000000;
            var gasBudgetSatoshis = gasPriceSatoshis * gasLimit;
            var relayFeeSatoshis = 10000;
            var change = 200000;

            var totalSuppliedSatoshis = gasBudgetSatoshis + relayFeeSatoshis;

            var carrier = SmartContractCarrier.CallContract(1, 0, "TestMethod", (ulong)gasPriceSatoshis, (Gas)gasLimit);
            var serialized = carrier.Serialize();

            Transaction funding = new Transaction
            {
                Outputs =
                {
                    new TxOut(totalSuppliedSatoshis + change, new Script())
                }
            };

            var transactionBuilder = new TransactionBuilder();
            transactionBuilder.AddCoins(funding);
            transactionBuilder.SendFees(totalSuppliedSatoshis);
            transactionBuilder.Send(new Script(serialized), 0);

            // Add a change output to the transaction
            transactionBuilder.SetChange(new Script());

            Transaction transaction = transactionBuilder.BuildTransaction(false);

            context.BlockValidationContext.Block.Transactions = new List<Transaction>
            {
                transaction
            };

            await rule.RunAsync(context);
        }

        /// <summary>
        /// In this test we supply a higher gas limit in our carrier than what we budgeted for in our transaction
        /// </summary>
        [Fact]
        public void GasBudgetRule_FailureAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            GasBudgetRule rule = testContext.CreateRule<GasBudgetRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus,
                testContext.Chain.Tip);

            context.BlockValidationContext.Block = new Block();

            var gasPriceSatoshis = 20;
            var gasLimit = 4000000;
            var gasBudgetSatoshis = gasPriceSatoshis * gasLimit;
            var relayFeeSatoshis = 10000;

            var totalSuppliedSatoshis = gasBudgetSatoshis + relayFeeSatoshis;

            var higherGasLimit = gasLimit + 10000;

            var carrier = SmartContractCarrier.CallContract(1, 0, "TestMethod", (ulong)gasPriceSatoshis, (Gas)higherGasLimit);
            var serialized = carrier.Serialize();

            Transaction funding = new Transaction
            {
                Outputs =
                {
                    new TxOut(totalSuppliedSatoshis, new Script())
                }
            };

            var transactionBuilder = new TransactionBuilder();
            transactionBuilder.AddCoins(funding);
            transactionBuilder.SendFees(relayFeeSatoshis);
            transactionBuilder.Send(new Script(serialized), gasBudgetSatoshis);

            Transaction transaction = transactionBuilder.BuildTransaction(false);

            context.BlockValidationContext.Block.Transactions = new List<Transaction>
            {
                transaction
            };

            Task<ConsensusErrorException> error = Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }
    }
}