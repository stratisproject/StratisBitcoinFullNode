using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Patricia;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.ResultProcessors;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public sealed class ContractExecutorTests
    {
        private const ulong BlockHeight = 0;
        private static readonly uint160 CoinbaseAddress = 0;
        private static readonly uint160 ToAddress = 1;
        private static readonly uint160 SenderAddress = 2;
        private static readonly Money MempoolFee = new Money(1_000_000); 
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly IContractRefundProcessor refundProcessor;
        private readonly IStateRepositoryRoot state;
        private readonly IContractTransferProcessor transferProcessor;
        private readonly SmartContractValidator validator;
        private IInternalExecutorFactory internalTxExecutorFactory;
        private IVirtualMachine vm;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly StateFactory stateFactory;
        private readonly IAddressGenerator addressGenerator;
        private readonly ILoader assemblyLoader;
        private readonly IContractModuleDefinitionReader moduleDefinitionReader;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;
        private readonly IStateProcessor stateProcessor;
        private readonly ISmartContractStateFactory smartContractStateFactory;
        private readonly ISerializer serializer;

        public ContractExecutorTests()
        {
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = new SmartContractsRegTest();
            this.refundProcessor = new ContractRefundProcessor(this.loggerFactory);
            this.state = new StateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.transferProcessor = new ContractTransferProcessor(this.loggerFactory, this.network);
            this.validator = new SmartContractValidator();
            this.addressGenerator = new AddressGenerator();
            this.assemblyLoader = new ContractAssemblyLoader();
            this.moduleDefinitionReader = new ContractModuleDefinitionReader();
            this.contractPrimitiveSerializer = new ContractPrimitiveSerializer(this.network);
            this.serializer = new Serializer(this.contractPrimitiveSerializer);
            this.vm = new ReflectionVirtualMachine(this.validator, this.loggerFactory, this.assemblyLoader, this.moduleDefinitionReader);
            this.stateProcessor = new StateProcessor(this.vm, this.addressGenerator);
            this.internalTxExecutorFactory = new InternalExecutorFactory(this.loggerFactory, this.stateProcessor);
            this.smartContractStateFactory = new SmartContractStateFactory(this.contractPrimitiveSerializer, this.internalTxExecutorFactory, this.serializer);
            
            this.callDataSerializer = new CallDataSerializer(this.contractPrimitiveSerializer);

            this.stateFactory = new StateFactory(this.smartContractStateFactory);
        }

        [Fact]
        public void SmartContractExecutor_CallContract_Fails_ReturnFundsToSender()
        {
            //Get the contract execution code------------------------
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ThrowExceptionContract.cs");
            Assert.True(compilationResult.Success);
            byte[] contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------


            //Call smart contract and add to transaction-------------
            var contractTxData = new ContractTxData(1, 1, (RuntimeObserver.Gas)500_000, ToAddress, "ThrowException");
            var transactionCall = new Transaction();
            TxOut callTxOut = transactionCall.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));
            callTxOut.Value = 100;
            //-------------------------------------------------------

            //Deserialize the contract from the transaction----------
            //and get the module definition
            //-------------------------------------------------------
            this.state.SetCode(new uint160(1), contractExecutionCode);
            this.state.SetContractType(new uint160(1), "ThrowExceptionContract");

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transactionCall);
            
            var executor = new ContractExecutor(
                this.callDataSerializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);

            Assert.True(result.Revert);
            Assert.NotNull(result.InternalTransaction);
            Assert.Single(result.InternalTransaction.Inputs);
            Assert.Single(result.InternalTransaction.Outputs);

            var actualSender = new uint160(result.InternalTransaction.Outputs[0].ScriptPubKey.GetDestination(this.network).ToBytes());
            Assert.Equal(SenderAddress, actualSender);
            Assert.Equal(100, result.InternalTransaction.Outputs[0].Value);
        }

        [Fact]
        public void SmartContractExecutor_CallContract_DoesNotExist_Refund()
        {
            var contractTxData = new ContractTxData(1, 1, (RuntimeObserver.Gas) 10000, ToAddress, "TestMethod");

            var transaction = new Transaction();
            TxOut txOut = transaction.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));
            txOut.Value = 100;

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), transaction);

            var executor = new ContractExecutor(
                this.callDataSerializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);
            Assert.True(result.Revert);
        }

        [Fact]
        public void SME_CreateContract_ConstructorFails_Refund()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ContractConstructorInvalid.cs");
            Assert.True(compilationResult.Success);
            byte[] contractCode = compilationResult.Compilation;

            var contractTxData = new ContractTxData(0, (RuntimeObserver.Gas) 1, (RuntimeObserver.Gas)500_000, contractCode);
            var tx = new Transaction();
            tx.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new ContractExecutor(
                this.callDataSerializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);

            Assert.NotNull(result.ErrorMessage);
            // Number here shouldn't be hardcoded - note this is really only to let us know of consensus failure
            Assert.Equal(GasPriceList.CreateCost + 18, result.GasConsumed);
        }

        [Fact]
        public void SME_CreateContract_MethodParameters_InvalidParameterCount()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ContractInvalidParameterCount.cs");
            Assert.True(compilationResult.Success);
            byte[] contractCode = compilationResult.Compilation;

            object[] methodParameters = { 5 };

            var contractTxData = new ContractTxData(0, (RuntimeObserver.Gas)1, (RuntimeObserver.Gas)500_000, contractCode, methodParameters);
            var tx = new Transaction();
            tx.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new ContractExecutor(
                this.callDataSerializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);
            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(GasPriceList.CreateCost, result.GasConsumed);
        }

        [Fact]
        public void SME_CreateContract_MethodParameters_ParameterTypeMismatch()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ContractMethodParameterTypeMismatch.cs");
            Assert.True(compilationResult.Success);
            byte[] contractCode = compilationResult.Compilation;

            object[] methodParameters = { true };

            var contractTxData = new ContractTxData(0, (RuntimeObserver.Gas)1, (RuntimeObserver.Gas)500_000, contractCode, methodParameters);
            var tx = new Transaction();
            tx.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new ContractExecutor(
                this.callDataSerializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);

            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(GasPriceList.CreateCost, result.GasConsumed);
        }

        [Fact]
        public void Execute_InterContractCall_Internal_InfiniteLoop_Fails()
        {
            // Create contract 1

            //Get the contract execution code------------------------
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/InfiniteLoop.cs");
            Assert.True(compilationResult.Success);
            byte[] contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------

            // Add contract creation code to transaction-------------
            var contractTxData = new ContractTxData(1, (RuntimeObserver.Gas)1, (RuntimeObserver.Gas)500_000, contractExecutionCode);
            var transaction = new Transaction();
            TxOut txOut = transaction.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));
            txOut.Value = 100;
            //-------------------------------------------------------

            //Deserialize the contract from the transaction----------
            //and get the module definition
            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            var executor = new ContractExecutor(
                this.callDataSerializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);
            uint160 address1 = result.NewContractAddress;
            //-------------------------------------------------------


            // Create contract 2

            //Get the contract execution code------------------------
            compilationResult = ContractCompiler.CompileFile("SmartContracts/CallInfiniteLoopContract.cs");
            Assert.True(compilationResult.Success);

            contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------

            //Call smart contract and add to transaction-------------
            contractTxData = new ContractTxData(1, (RuntimeObserver.Gas)1, (RuntimeObserver.Gas)500_000, contractExecutionCode);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));
            txOut.Value = 100;
            //-------------------------------------------------------

            //Deserialize the contract from the transaction----------
            //and get the module definition
            //-------------------------------------------------------

            transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            result = executor.Execute(transactionContext);

            uint160 address2 = result.NewContractAddress;

            // Invoke infinite loop

            var gasLimit = (RuntimeObserver.Gas)500_000;

            object[] parameters = { address1.ToAddress() };

            contractTxData = new ContractTxData(1, (RuntimeObserver.Gas)1, gasLimit, address2, "CallInfiniteLoop", parameters);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));
            txOut.Value = 100;

            transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            var callExecutor = new ContractExecutor(
                this.callDataSerializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            // Because our contract contains an infinite loop, we want to kill our test after
            // some amount of time without achieving a result. 3 seconds is an arbitrarily high enough timeout
            // for the method body to have finished execution while minimising the amount of time we spend 
            // running tests
            // If you're running with the debugger on this will obviously be a source of failures
            result = TimeoutHelper.RunCodeWithTimeout(3, () => callExecutor.Execute(transactionContext));

            // Actual call was successful, but internal call failed due to gas - returned false.
            Assert.False(result.Revert);
            Assert.False((bool) result.Return);
        }

        [Fact]
        public void Execute_NestedLoop_ExecutionSucceeds()
        {
            AssertSuccessfulContractMethodExecution(nameof(NestedLoop), nameof(NestedLoop.GetNumbers), new object[] { (int)6 }, "1; 1,2; 1,2,3; 1,2,3,4; 1,2,3,4,5; 1,2,3,4,5,6; ");
        }

        [Fact]
        public void Execute_MultipleIfElseBlocks_ExecutionSucceeds()
        {
            AssertSuccessfulContractMethodExecution(nameof(MultipleIfElseBlocks), nameof(MultipleIfElseBlocks.PersistNormalizeValue), new object[] { "z" });
        }

        private void AssertSuccessfulContractMethodExecution(string contractName, string methodName, object[] methodParameters = null, string expectedReturn = null)
        {
            var transactionValue = (Money)100;

            var executor = new ContractExecutor(
                this.callDataSerializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile($"SmartContracts/{contractName}.cs");
            Assert.True(compilationResult.Success);
            byte[] contractExecutionCode = compilationResult.Compilation;

            var contractTxData = new ContractTxData(1, (RuntimeObserver.Gas)1, (RuntimeObserver.Gas)500_000, contractExecutionCode);

            var transaction = new Transaction();
            TxOut txOut = transaction.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));
            txOut.Value = transactionValue;
            var transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            IContractExecutionResult result = executor.Execute(transactionContext);
            uint160 contractAddress = result.NewContractAddress;

            contractTxData = new ContractTxData(1, (RuntimeObserver.Gas)1, (RuntimeObserver.Gas)500_000, contractAddress, methodName, methodParameters);

            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));
            txOut.Value = transactionValue;
            transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            result = executor.Execute(transactionContext);

            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            if (expectedReturn != null)
            {
                Assert.NotNull(result.Return);
                Assert.Equal(expectedReturn, (string)result.Return);
            }
        }
    }
}
