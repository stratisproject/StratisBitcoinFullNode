using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.ContractValidation;
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

            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(contractExecutionCode);
            //-------------------------------------------------------

            this.stateRepository.SetCode(new uint160(1), contractExecutionCode);

            var executor = new SmartContractTransactionExecutor(this.stateRepository, this.decompiler, new SmartContractValidator(new ISmartContractValidator[] { }), this.gasInjector, deserializedCall, 0, deserializedCall.To, this.network);

            ISmartContractExecutionResult result = executor.Execute();

            Assert.True(result.Revert);
            Assert.Single(result.InternalTransactions);
            Assert.Single(result.InternalTransactions[0].Inputs);
            Assert.Single(result.InternalTransactions[0].Outputs);

            var actualSender = new uint160(result.InternalTransactions[0].Outputs[0].ScriptPubKey.GetDestination().ToBytes());
            Assert.Equal(senderAddress, actualSender);
            Assert.Equal(100, result.InternalTransactions[0].Outputs[0].Value);
        }
    }
}