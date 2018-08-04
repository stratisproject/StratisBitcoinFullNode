using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class SmartContractExecutorTests
    {
        private const ulong BlockHeight = 0;
        private static readonly uint160 CoinbaseAddress = 0;
        private static readonly uint160 ToAddress = 1;
        private static readonly uint160 SenderAddress = 2;
        private static readonly Money MempoolFee = new Money(10000);
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly ISmartContractResultRefundProcessor refundProcessor;
        private readonly IContractStateRepository state;
        private readonly ISmartContractResultTransferProcessor transferProcessor;
        private readonly SmartContractValidator validator;
        private InternalTransactionExecutorFactory internalTxExecutorFactory;
        private ReflectionVirtualMachine vm;
        private ICallDataSerializer serializer;

        public SmartContractExecutorTests()
        {
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = new SmartContractsRegTest();
            this.refundProcessor = new SmartContractResultRefundProcessor(this.loggerFactory);
            this.state = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.transferProcessor = new SmartContractResultTransferProcessor(DateTimeProvider.Default, this.loggerFactory, this.network);
            this.validator = new SmartContractValidator();
            this.internalTxExecutorFactory = new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            this.validator = new SmartContractValidator();
            this.vm = new ReflectionVirtualMachine(this.validator, this.internalTxExecutorFactory, this.loggerFactory, this.network);
            this.serializer = CallDataSerializer.Default;
        }


        [Fact]
        public void MappingAndListTests()
        {
            // Create contract
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/Mappings.cs");
            Assert.True(compilationResult.Success);
            byte[] contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------

            // Add contract creation code to transaction-------------
            var carrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, (Gas)5000);
            var transaction = new Transaction();
            TxOut txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;
            //-------------------------------------------------------

            //Deserialize the contract from the transaction----------
            //and get the module definition
            ISmartContractTransactionContext transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            var executor = new Executor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.vm);

            ISmartContractExecutionResult result = executor.Execute(transactionContext);
            uint160 address1 = result.NewContractAddress;
            //-------------------------------------------------------

            // Basic Mapping Test
            var gasLimit = (Gas)200_000;
            carrier = SmartContractCarrier.CallContract(1, address1, "BasicMappingTest", 1, gasLimit);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;

            transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            var callExecutor = new Executor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.vm);

            result = callExecutor.Execute(transactionContext);
            Assert.Equal("Value1", result.Return);


            // Basic List Test
            carrier = SmartContractCarrier.CallContract(1, address1, "BasicListTest", 1, gasLimit);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;

            transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            callExecutor = new Executor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.vm);

            result = callExecutor.Execute(transactionContext);
            Assert.Equal("Value1", result.Return);


            // Mapping of lists test 
            carrier = SmartContractCarrier.CallContract(1, address1, "MappingOfListsTest", 1, gasLimit);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;

            transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            callExecutor = new Executor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.vm);

            result = callExecutor.Execute(transactionContext);
            Assert.Equal("Value1", result.Return);


            // List of structs with mappings test
            carrier = SmartContractCarrier.CallContract(1, address1, "ListOfStructsWithMappingsTest", 1, gasLimit);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;

            transactionContext = new SmartContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, SenderAddress, transaction);

            callExecutor = new Executor(this.loggerFactory,
                this.serializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.vm);

            result = callExecutor.Execute(transactionContext);
            Assert.Equal("Value1", result.Return);
        }

       
    }
}