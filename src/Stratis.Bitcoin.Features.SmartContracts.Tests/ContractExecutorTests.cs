using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Loader;
using Stratis.SmartContracts.Executor.Reflection.ResultProcessors;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
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
        private readonly ICallDataSerializer serializer;
        private readonly StateFactory stateFactory;
        private readonly IAddressGenerator addressGenerator;
        private readonly ILoader assemblyLoader;
        private readonly IContractModuleDefinitionReader moduleDefinitionReader;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;
        private readonly IStateProcessor stateProcessor;
        private readonly ISmartContractStateFactory smartContractStateFactory;

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
            this.vm = new ReflectionVirtualMachine(this.validator, this.loggerFactory, this.network, this.assemblyLoader, this.moduleDefinitionReader);
            this.stateProcessor = new StateProcessor(this.vm, this.addressGenerator);
            this.internalTxExecutorFactory = new InternalExecutorFactory(this.loggerFactory, this.network, this.stateProcessor);
            this.smartContractStateFactory = new SmartContractStateFactory(this.contractPrimitiveSerializer, this.network, this.internalTxExecutorFactory);
            
            this.serializer = new CallDataSerializer(new MethodParameterStringSerializer());

            this.stateFactory = new StateFactory(this.network, this.smartContractStateFactory);
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
            var contractTxData = new ContractTxData(1, 1, (Gas)5000, ToAddress, "ThrowException");
            var transactionCall = new Transaction();
            TxOut callTxOut = transactionCall.AddOutput(0, new Script(this.serializer.Serialize(contractTxData)));
            callTxOut.Value = 100;
            //-------------------------------------------------------

            //Deserialize the contract from the transaction----------
            //and get the module definition
            //-------------------------------------------------------
            this.state.SetCode(new uint160(1), contractExecutionCode);
            this.state.SetContractType(new uint160(1), "ThrowExceptionContract");

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transactionCall);
            
            var executor = new ContractExecutor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.network,
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
            var contractTxData = new ContractTxData(1, 1, (Gas) 10000, ToAddress, "TestMethod");

            var transaction = new Transaction();
            TxOut txOut = transaction.AddOutput(0, new Script(this.serializer.Serialize(contractTxData)));
            txOut.Value = 100;

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), transaction);

            var executor = new ContractExecutor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.network,
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

            var contractTxData = new ContractTxData(0, (Gas) 1, (Gas)10000, contractCode);
            var tx = new Transaction();
            tx.AddOutput(0, new Script(this.serializer.Serialize(contractTxData)));

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new ContractExecutor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.network,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);

            Assert.NotNull(result.ErrorMessage);
            // Base cost + constructor cost (21 because that is number of gas to invoke Assert(false));
            Assert.Equal(GasPriceList.BaseCost + 21, result.GasConsumed);
        }

        [Fact]
        public void SME_CreateContract_MethodParameters_InvalidParameterCount()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ContractInvalidParameterCount.cs");
            Assert.True(compilationResult.Success);
            byte[] contractCode = compilationResult.Compilation;

            object[] methodParameters = { 5 };

            var contractTxData = new ContractTxData(0, (Gas)1, (Gas)10000, contractCode, methodParameters);
            var tx = new Transaction();
            tx.AddOutput(0, new Script(this.serializer.Serialize(contractTxData)));

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new ContractExecutor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.network,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);
            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void SME_CreateContract_MethodParameters_ParameterTypeMismatch()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ContractMethodParameterTypeMismatch.cs");
            Assert.True(compilationResult.Success);
            byte[] contractCode = compilationResult.Compilation;

            object[] methodParameters = { true };

            var contractTxData = new ContractTxData(0, (Gas)1, (Gas)10000, contractCode, methodParameters);
            var tx = new Transaction();
            tx.AddOutput(0, new Script(this.serializer.Serialize(contractTxData)));

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new ContractExecutor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.network,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);

            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
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
            var contractTxData = new ContractTxData(1, (Gas)1, (Gas)3500, contractExecutionCode);
            var transaction = new Transaction();
            TxOut txOut = transaction.AddOutput(0, new Script(this.serializer.Serialize(contractTxData)));
            txOut.Value = 100;
            //-------------------------------------------------------

            //Deserialize the contract from the transaction----------
            //and get the module definition
            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            var executor = new ContractExecutor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.network,
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
            contractTxData = new ContractTxData(1, (Gas)1, (Gas)3500, contractExecutionCode);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(this.serializer.Serialize(contractTxData)));
            txOut.Value = 100;
            //-------------------------------------------------------

            //Deserialize the contract from the transaction----------
            //and get the module definition
            //-------------------------------------------------------

            transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            result = executor.Execute(transactionContext);

            uint160 address2 = result.NewContractAddress;

            // Invoke infinite loop

            var gasLimit = (Gas)100_000;

            object[] parameters = { address1.ToAddress(this.network).Value };

            contractTxData = new ContractTxData(1, (Gas)1, gasLimit, address2, "CallInfiniteLoop", parameters);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(this.serializer.Serialize(contractTxData)));
            txOut.Value = 100;

            transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            var callExecutor = new ContractExecutor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.network,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            // Because our contract contains an infinite loop, we want to kill our test after
            // some amount of time without achieving a result. 3 seconds is an arbitrarily high enough timeout
            // for the method body to have finished execution while minimising the amount of time we spend 
            // running tests
            // If you're running with the debugger on this will obviously be a source of failures
            result = RunWithTimeout(3, () => callExecutor.Execute(transactionContext));

            // Actual call was successful, but internal call failed due to gas - returned false.
            Assert.False(result.Revert);
            Assert.False((bool) result.Return);
        }

        private static T RunWithTimeout<T>(int timeout, Func<T> execute)
        {
            // ref. https://stackoverflow.com/questions/20282111/xunit-net-how-can-i-specify-a-timeout-how-long-a-test-should-maximum-need
            // Only run single-threaded code in this method

            Task<T> task = Task.Run(execute);
            bool completedInTime = Task.WaitAll(new Task[] { task }, TimeSpan.FromSeconds(timeout));

            if (task.Exception != null)
            {
                if (task.Exception.InnerExceptions.Count == 1)
                {
                    throw task.Exception.InnerExceptions[0];
                }

                throw task.Exception;
            }

            if (!completedInTime)
            {
                throw new TimeoutException($"Task did not complete in {timeout} seconds.");
            }

            return task.Result;
        }
    }
}
