using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class SmartContractTransactionExecutorTests
    {
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly ContractStateRepositoryRoot stateRepository;
        private readonly ISmartContractReceiptStorage receiptStorage;

        public SmartContractTransactionExecutorTests()
        {
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();

            this.network = Network.SmartContractsRegTest;
            this.stateRepository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.receiptStorage = Mock.Of<ISmartContractReceiptStorage>();
        }

        [Fact]
        public void ExecuteCallContract_Fails_ReturnFundsToSender()
        {
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/ThrowExceptionContract.cs");
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
            var deserializedCall = SmartContractCarrier.Deserialize(transactionCall, callTxOut);
            deserializedCall.Sender = senderAddress;
            //-------------------------------------------------------

            this.stateRepository.SetCode(new uint160(1), contractExecutionCode);

            var executor = SmartContractExecutor.Initialize(deserializedCall, this.network, this.receiptStorage, this.stateRepository, new SmartContractValidator(new ISmartContractValidator[] { }), this.keyEncodingStrategy, this.loggerFactory, new Money(10000));
            ISmartContractExecutionResult result = executor.Execute(0, deserializedCall.ContractAddress);

            Assert.True(result.Revert);
            Assert.NotNull(result.InternalTransaction);
            Assert.Single(result.InternalTransaction.Inputs);
            Assert.Single(result.InternalTransaction.Outputs);

            var actualSender = new uint160(result.InternalTransaction.Outputs[0].ScriptPubKey.GetDestination(this.network).ToBytes());
            Assert.Equal(senderAddress, actualSender);
            Assert.Equal(100, result.InternalTransaction.Outputs[0].Value);
        }

        public void ExecuteCreateContract_ValidationFails_RefundGas_MempoolFeeLessGas()
        {
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/ContractFailsValidation.cs");
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
            var deserializedCreate = SmartContractCarrier.Deserialize(transaction, txOut);
            deserializedCreate.Sender = senderAddress;
            //-------------------------------------------------------

            this.stateRepository.SetCode(new uint160(1), contractExecutionCode);
            var validator = new SmartContractValidator(new ISmartContractValidator[] { new SmartContractDeterminismValidator() });

            var executor = SmartContractExecutor.Initialize(deserializedCreate, this.network, this.receiptStorage, this.stateRepository, validator, this.keyEncodingStrategy, this.loggerFactory, new Money(10000));
            ISmartContractExecutionResult result = executor.Execute(0, deserializedCreate.GetNewContractAddress());

            Assert.True(result.Revert);
            Assert.Equal((ulong)7500, result.Fee);
            Assert.Single(result.Refunds);
            Assert.Equal(2500, result.Refunds.First().Value);
        }

        [Fact]
        public void Executor_Contract_DoesNotExist_Refund()
        {
            var state = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            var toAddress = new uint160(1);
            var carrier = SmartContractCarrier.CallContract(1, toAddress, "TestMethod", 1, (Gas)1000000);
            carrier.Sender = new uint160(2);
            var executor = new CallSmartContract(carrier, this.keyEncodingStrategy, this.loggerFactory, new Money(10000000), this.network, this.receiptStorage, state, new SmartContractValidator(new ISmartContractValidator[] { }));
            ISmartContractExecutionResult result = executor.Execute(0, toAddress);
            Assert.IsType<SmartContractDoesNotExistException>(result.Exception);
        }

        [Fact]
        public void Execute_InterContractCall_InfiniteLoop_AllGasConsumed()
        {
            // Create contract 1

            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/InfiniteLoop.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------

            //Call smart contract and add to transaction-------------
            var carrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, (Gas)3500);
            var transaction = new Transaction();
            TxOut txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;
            //-------------------------------------------------------

            var senderAddress = new uint160(2);

            //Deserialize the contract from the transaction----------
            //and get the module definition
            var deserializedCreate = SmartContractCarrier.Deserialize(transaction, txOut);
            deserializedCreate.Sender = senderAddress;
            //-------------------------------------------------------

            var validator = new SmartContractValidator(new ISmartContractValidator[] { new SmartContractDeterminismValidator() });

            var executor = SmartContractExecutor.Initialize(deserializedCreate, this.network, this.receiptStorage, this.stateRepository, validator, this.keyEncodingStrategy, this.loggerFactory, new Money(10000));
            ISmartContractExecutionResult result = executor.Execute(0, deserializedCreate.GetNewContractAddress());

            uint160 address1 = result.NewContractAddress;

            // Create contract 2

            //Get the contract execution code------------------------
            compilationResult = SmartContractCompiler.CompileFile("SmartContracts/CallInfiniteLoopContract.cs");
            Assert.True(compilationResult.Success);

            contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------

            //Call smart contract and add to transaction-------------
            carrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, (Gas)3500);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;
            //-------------------------------------------------------

            senderAddress = new uint160(3);

            //Deserialize the contract from the transaction----------
            //and get the module definition
            deserializedCreate = SmartContractCarrier.Deserialize(transaction, txOut);
            deserializedCreate.Sender = senderAddress;
            //-------------------------------------------------------

            validator = new SmartContractValidator(new ISmartContractValidator[] { new SmartContractDeterminismValidator() });

            executor = SmartContractExecutor.Initialize(deserializedCreate, this.network, this.receiptStorage, this.stateRepository, validator, this.keyEncodingStrategy, this.loggerFactory, new Money(10000));
            result = executor.Execute(0, deserializedCreate.GetNewContractAddress());

            uint160 address2 = result.NewContractAddress;

            // Invoke infinite loop

            var gasLimit = (Gas)1000000;

            string[] parameters =
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, address1.ToAddress(this.network).Value),
            };

            carrier = SmartContractCarrier.CallContract(1, address2, "CallInfiniteLoop", 1, gasLimit, parameters);
            transaction = new Transaction();
            txOut = transaction.AddOutput(0, new Script(carrier.Serialize()));
            txOut.Value = 100;

            var deserializedCall = SmartContractCarrier.Deserialize(transaction, txOut);
            deserializedCall.Sender = senderAddress;

            executor = SmartContractExecutor.Initialize(deserializedCall, this.network, this.receiptStorage, this.stateRepository, validator, this.keyEncodingStrategy, this.loggerFactory, new Money(10000));

            uint160 someCoinbaseAddress = deserializedCall.GetNewContractAddress();

            // Because our contract contains an infinite loop, we want to kill our test after
            // some amount of time without achieving a result. 3 seconds is an arbitrarily high enough timeout
            // for the method body to have finished execution while minimising the amount of time we spend 
            // running tests
            // If you're running with the debugger on this will obviously be a source of failures
            result = RunWithTimeout(3, () => executor.Execute(0, someCoinbaseAddress));

            Assert.IsType<OutOfGasException>(result.Exception);
            Assert.Equal(gasLimit, result.GasConsumed);
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