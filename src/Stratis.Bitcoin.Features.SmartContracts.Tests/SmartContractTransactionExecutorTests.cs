using System.Linq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class SmartContractTransactionExecutorTests
    {
        private readonly SmartContractDecompiler decompiler;
        private readonly ISmartContractGasInjector gasInjector;
        private readonly ContractStateRepositoryRoot stateRepository;
        private readonly Network network;

        public SmartContractTransactionExecutorTests()
        {
            this.decompiler = new SmartContractDecompiler();
            this.gasInjector = new SmartContractGasInjector();
            this.stateRepository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource())); ;
            this.network = Network.SmartContractsRegTest;
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

            var executor = SmartContractExecutor.Initialize(deserializedCall, this.decompiler, this.gasInjector, this.network, this.stateRepository, new SmartContractValidator(new ISmartContractValidator[] { }));
            ISmartContractExecutionResult result = executor.Execute(0, deserializedCall.ContractAddress);

            Assert.True(result.Revert);
            Assert.NotNull(result.InternalTransaction);
            Assert.Single(result.InternalTransaction.Inputs);
            Assert.Single(result.InternalTransaction.Outputs);

            var actualSender = new uint160(result.InternalTransaction.Outputs[0].ScriptPubKey.GetDestination().ToBytes());
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

            var executor = SmartContractExecutor.Initialize(deserializedCreate, this.decompiler, this.gasInjector, this.network, this.stateRepository, validator);
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

            var executor = new CallSmartContract(carrier, this.decompiler, this.gasInjector, this.network, state, new SmartContractValidator(new ISmartContractValidator[] { }));
            ISmartContractExecutionResult result = executor.Execute(0, toAddress);
            Assert.IsType<SmartContractDoesNotExistException>(result.Exception);
        }
    }
}