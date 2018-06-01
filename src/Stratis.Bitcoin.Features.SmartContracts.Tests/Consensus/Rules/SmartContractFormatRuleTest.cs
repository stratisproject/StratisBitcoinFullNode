using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class SmartContractFormatRuleTest
    {
        private readonly Network network;

        public SmartContractFormatRuleTest()
        {
            this.network = Network.SmartContractsRegTest;
        }

        private UnspentOutputSet GetMockOutputSet()
        {
            var fakeTxOut = new TxOut
            {
                Value = 100_000_000
            };

            var unspentOutputMock = new Mock<UnspentOutputSet>();
            unspentOutputMock.Setup(x => x.AccessCoins(It.IsAny<uint256>())).Returns(new UnspentOutputs()
            {
                Outputs = new TxOut[]
                {
                    fakeTxOut
                }
            });

            return unspentOutputMock.Object;
        }

        [Fact]
        public void SmartContractFormatRule_GasBudgetMaxDoesntOverflow()
        {
            checked
            {
#pragma warning disable CS0219 // Variable is assigned but its value is never used
                ulong test = SmartContractFormatRule.GasPriceMaximum * SmartContractFormatRule.GasLimitMaximum;
#pragma warning restore CS0219 // Variable is assigned but its value is never used
            }
        }

        [Fact]
        public async Task SmartContractFormatRule_SuccessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            SmartContractFormatRule rule = testContext.CreateRule<SmartContractFormatRule>();

            var context = new RuleContext(new BlockValidationContext(), testContext.Network.Consensus,
                testContext.Chain.Tip)
            {
                Set = GetMockOutputSet()
            };

            context.BlockValidationContext.Block = testContext.Network.Consensus.ConsensusFactory.CreateBlock();

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

            var transactionBuilder = new TransactionBuilder(testContext.Network);
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
        public async Task SmartContractFormatRule_MultipleOutputs_SuccessAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            SmartContractFormatRule rule = testContext.CreateRule<SmartContractFormatRule>();

            var context = new RuleContext(new BlockValidationContext(), testContext.Network.Consensus, testContext.Chain.Tip)
            {
                Set = GetMockOutputSet()
            };

            context.BlockValidationContext.Block = testContext.Network.Consensus.ConsensusFactory.CreateBlock();

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

            var transactionBuilder = new TransactionBuilder(testContext.Network);
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
        public void SmartContractFormatRule_FailureAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            SmartContractFormatRule rule = testContext.CreateRule<SmartContractFormatRule>();

            var context = new RuleContext(new BlockValidationContext(), testContext.Network.Consensus, testContext.Chain.Tip);

            context.BlockValidationContext.Block = testContext.Network.Consensus.ConsensusFactory.CreateBlock();

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

            var transactionBuilder = new TransactionBuilder(testContext.Network);
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