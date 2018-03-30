using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
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
        private readonly IKeyEncodingStrategy keyEncodingStrategy;

        public SmartContractTransactionExecutorTests()
        {
            this.decompiler = new SmartContractDecompiler();
            this.gasInjector = new SmartContractGasInjector();
            this.stateRepository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource())); ;
            this.network = Network.SmartContractsRegTest;
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
        }

        [Fact]
        public void ExecuteCallContract_Fails_ReturnFundsToSender()
        {
            //Get the contract execution code------------------------
            byte[] contractExecutionCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowSystemExceptionContract.cs");
            //-------------------------------------------------------

            var toAddress = new uint160(1);

            //Call smart contract and add to transaction-------------
            var carrier = SmartContractCarrier.CallContract(1, toAddress, "ThrowException", 1, (Gas)500000);
            var transactionCall = new Transaction();
            //transactionCall.AddInput(new TxIn());
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


            var executor = SmartContractExecutor.InitializeForBlockAssembler(deserializedCall, this.decompiler, this.gasInjector, new Money(10000), this.network, this.stateRepository, new SmartContractValidator(new ISmartContractValidator[] { }), this.keyEncodingStrategy);
            ISmartContractExecutionResult result = executor.Execute(0, deserializedCall.ContractAddress);

            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(contractExecutionCode);
            //-------------------------------------------------------

            this.stateRepository.SetCode(new uint160(1), contractExecutionCode);

            //var executor = SmartContractExecutor.InitializeForBlockAssembler(deserializedCreate, this.decompiler, this.gasInjector, new Money(10000), this.network, this.stateRepository, validator, this.keyEncodingStrategy);
            //ISmartContractExecutionResult result = executor.Execute(0, deserializedCreate.GetNewContractAddress());

            Assert.True(result.Revert);
            Assert.NotNull(result.InternalTransaction);
            Assert.Single(result.InternalTransaction.Inputs);
            Assert.Single(result.InternalTransaction.Outputs);

            var actualSender = new uint160(result.InternalTransaction.Outputs[0].ScriptPubKey.GetDestination().ToBytes());
            Assert.Equal(senderAddress, actualSender);
            Assert.Equal(100, result.InternalTransaction.Outputs[0].Value);
        }

        [Fact]
        public void Executor_Contract_DoesNotExist_Refund()
        {
            var state = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            var toAddress = new uint160(1);
            var carrier = SmartContractCarrier.CallContract(1, toAddress, "TestMethod", 1, (Gas)100);

            var executor = new CallSmartContract(carrier, this.decompiler, this.gasInjector, this.network, state, new SmartContractValidator(new ISmartContractValidator[] { }), this.keyEncodingStrategy);
            ISmartContractExecutionResult result = executor.Execute(0, toAddress);
            Assert.IsType<SmartContractDoesNotExistException>(result.Exception);
        }
    }
}