using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.ReflectionExecutor;
using Stratis.SmartContracts.ReflectionExecutor.Compilation;
using Stratis.SmartContracts.ReflectionExecutor.Exceptions;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class SmartContractExecutorTests
    {
        private const ulong BlockHeight = 0;
        private static readonly uint160 CoinbaseAddress = 0;
        private static readonly Money MempoolFee = new Money(10000); 
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly IContractStateRepository state;
        private readonly SmartContractValidator validator;
        private readonly ISmartContractReceiptStorage receiptStorage;
        public SmartContractExecutorTests()
        {
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = Network.SmartContractsRegTest;
            this.state = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.validator = new SmartContractValidator(new ISmartContractValidator[] { });
            this.receiptStorage = Mock.Of<ISmartContractReceiptStorage>();
        }

        [Fact]
        public void SME_CallContract_Fails_ReturnFundsToSender()
        {
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.CompileFile("SmartContracts/ThrowExceptionContract.cs");
            Assert.True(compilationResult.Success);
            byte[] contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------

            var toAddress = new uint160(1);

            //Call smart contract and add to transaction-------------
            var carrier = SmartContractCarrier.CallContract(1, toAddress, "ThrowException", 1, (Gas)5000);
            var transactionCall = new Transaction();
            TxOut callTxOut = transactionCall.AddOutput(0, new Script(carrier.Serialize()));
            callTxOut.Value = 100;
            //-------------------------------------------------------

            var senderAddress = new uint160(2);

            //Deserialize the contract from the transaction----------
            //and get the module definition
            //-------------------------------------------------------

            this.state.SetCode(new uint160(1), contractExecutionCode);

            ISmartContractTransactionContext transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, senderAddress, transactionCall);

            SmartContractExecutor executor = new CallSmartContract(
                this.keyEncodingStrategy,
                this.loggerFactory,
                this.network,
                this.receiptStorage,
                this.state,
                transactionContext,
                this.validator);
            ISmartContractExecutionResult result = executor.Execute();

            Assert.True(result.Revert);
            Assert.NotNull(result.InternalTransaction);
            Assert.Single(result.InternalTransaction.Inputs);
            Assert.Single(result.InternalTransaction.Outputs);

            var actualSender = new uint160(result.InternalTransaction.Outputs[0].ScriptPubKey.GetDestination(this.network).ToBytes());
            Assert.Equal(senderAddress, actualSender);
            Assert.Equal(100, result.InternalTransaction.Outputs[0].Value);
        }

        //[Fact]
        public void SME_CreateContract_ValidationFails_RefundGas_MempoolFeeLessGas()
        {
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.CompileFile("SmartContracts/ContractFailsValidation.cs");
            Assert.True(compilationResult.Success);
            byte[] contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------

            var toAddress = new uint160(1);

            //Call smart contract and add to transaction-------------
            var carrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, (Gas)3500);
            var transaction = new Transaction();
            TxOut txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;
            //-------------------------------------------------------

            var senderAddress = new uint160(2);

            //Deserialize the contract from the transaction----------
            //and get the module definition
            //-------------------------------------------------------

            this.state.SetCode(new uint160(1), contractExecutionCode);
            var validator = new SmartContractValidator(new ISmartContractValidator[] { new SmartContractDeterminismValidator() });

            ISmartContractTransactionContext txContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, senderAddress, transaction);

            SmartContractExecutor executor = new CallSmartContract(
                this.keyEncodingStrategy,
                this.loggerFactory,
                this.network,
                this.receiptStorage,
                this.state,
                txContext,
                this.validator
            );
            ISmartContractExecutionResult result = executor.Execute();

            Assert.True(result.Revert);
            Assert.Equal((ulong)7500, result.Fee);
            Assert.Single(result.Refunds);
            Assert.Equal(2500, result.Refunds.First().Value);
        }

        [Fact]
        public void SME_CallContract_DoesNotExist_Refund()
        {
            var state = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            var toAddress = new uint160(1);
            var carrier = SmartContractCarrier.CallContract(1, toAddress, "TestMethod", 1, (Gas) 10000);

            var transaction = new Transaction();
            TxOut txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;

            ISmartContractTransactionContext transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), transaction);

            var executor = new CallSmartContract(
                this.keyEncodingStrategy,
                this.loggerFactory,
                this.network,
                this.receiptStorage,
                this.state,
                transactionContext,
                this.validator
                );
            ISmartContractExecutionResult result = executor.Execute();
            Assert.IsType<SmartContractDoesNotExistException>(result.Exception);
        }

        [Fact]
        public void SME_CreateContract_ConstructorFails_Refund()
        {
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.CompileFile("SmartContracts/ContractConstructorInvalid.cs");
            Assert.True(compilationResult.Success);
            byte[] contractCode = compilationResult.Compilation;

            var carrier = SmartContractCarrier.CreateContract(0, contractCode, 1, (Gas)10000);
            var tx = new Transaction();
            TxOut txOut = tx.AddOutput(0, new Script(carrier.Serialize()));

            ISmartContractTransactionContext transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new CreateSmartContract(
                this.keyEncodingStrategy,
                this.loggerFactory,
                this.network,
                this.receiptStorage,
                this.state,
                transactionContext,
                this.validator
                );

            ISmartContractExecutionResult result = executor.Execute();

            Assert.NotNull(result.Exception);
            // Base cost + constructor cost
            Assert.Equal(GasPriceList.BaseCost + 13, result.GasConsumed);
        }

        [Fact]
        public void SME_CreateContract_MethodParameters_InvalidParameterCount()
        {
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.CompileFile("SmartContracts/ContractInvalidParameterCount.cs");
            Assert.True(compilationResult.Success);
            byte[] contractCode = compilationResult.Compilation;

            string[] methodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Short, 5),
            };

            SmartContractCarrier carrier = SmartContractCarrier.CreateContract(0, contractCode, 1, (Gas)10000, methodParameters);
            var tx = new Transaction();
            TxOut txOut = tx.AddOutput(0, new Script(carrier.Serialize()));

            ISmartContractTransactionContext transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new CreateSmartContract(
                this.keyEncodingStrategy,
                this.loggerFactory,
                this.network,
                this.receiptStorage,
                this.state,
                transactionContext,
                this.validator
            );
            ISmartContractExecutionResult result = executor.Execute();

            Assert.NotNull(result.Exception);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void SME_CreateContract_MethodParameters_ParameterTypeMismatch()
        {
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.CompileFile("SmartContracts/ContractMethodParameterTypeMismatch.cs");
            Assert.True(compilationResult.Success);
            byte[] contractCode = compilationResult.Compilation;

            string[] methodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Bool, true),
            };

            var carrier = SmartContractCarrier.CreateContract(0, contractCode, 1, (Gas)10000, methodParameters);
            var tx = new Transaction();
            TxOut txOut = tx.AddOutput(0, new Script(carrier.Serialize()));

            ISmartContractTransactionContext transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new CreateSmartContract(
                this.keyEncodingStrategy,
                this.loggerFactory,
                this.network,
                this.receiptStorage,
                this.state,
                transactionContext,
                this.validator
                );
            ISmartContractExecutionResult result = executor.Execute();

            Assert.NotNull(result.Exception);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }
    }
}