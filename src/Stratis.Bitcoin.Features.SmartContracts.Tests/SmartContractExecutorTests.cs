using System.Linq;
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
    public sealed class SmartContractExecutorTests
    {
        private readonly SmartContractDecompiler decompiler;
        private readonly ISmartContractGasInjector gasInjector;
        private readonly Network network;
        private readonly IContractStateRepository state;
        private readonly SmartContractValidator validator;

        public SmartContractExecutorTests()
        {
            this.decompiler = new SmartContractDecompiler();
            this.gasInjector = new SmartContractGasInjector();
            this.network = Network.SmartContractsRegTest;
            this.state = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.validator = new SmartContractValidator(new ISmartContractValidator[] { });
        }

        [Fact]
        public void SME_CallContract_Fails_ReturnFundsToSender()
        {
            //Get the contract execution code------------------------
            byte[] contractExecutionCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowSystemExceptionContract.cs");
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

            this.state.SetCode(new uint160(1), contractExecutionCode);

            SmartContractExecutor executor = SmartContractExecutor.Initialize(deserializedCall, this.decompiler, this.gasInjector, this.network, this.state, new SmartContractValidator(new ISmartContractValidator[] { }), new Money(10000));
            ISmartContractExecutionResult result = executor.Execute(0, deserializedCall.ContractAddress);

            Assert.True(result.Revert);
            Assert.NotNull(result.InternalTransaction);
            Assert.Single(result.InternalTransaction.Inputs);
            Assert.Single(result.InternalTransaction.Outputs);

            var actualSender = new uint160(result.InternalTransaction.Outputs[0].ScriptPubKey.GetDestination().ToBytes());
            Assert.Equal(senderAddress, actualSender);
            Assert.Equal(100, result.InternalTransaction.Outputs[0].Value);
        }

        //[Fact]
        public void SME_CreateContract_ValidationFails_RefundGas_MempoolFeeLessGas()
        {
            //Get the contract execution code------------------------
            byte[] contractExecutionCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ContractFailsValidation.cs");
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

            this.state.SetCode(new uint160(1), contractExecutionCode);
            var validator = new SmartContractValidator(new ISmartContractValidator[] { new SmartContractDeterminismValidator() });

            SmartContractExecutor executor = SmartContractExecutor.Initialize(deserializedCreate, this.decompiler, this.gasInjector, this.network, this.state, validator, new Money(10000));
            ISmartContractExecutionResult result = executor.Execute(0, deserializedCreate.GetNewContractAddress());

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
            var carrier = SmartContractCarrier.CallContract(1, toAddress, "TestMethod", 1, (Gas)10000);
            carrier.Sender = new uint160(2);

            var executor = new CallSmartContract(carrier, this.decompiler, this.gasInjector, this.network, state, new SmartContractValidator(new ISmartContractValidator[] { }), new Money(10000));
            ISmartContractExecutionResult result = executor.Execute(0, toAddress);
            Assert.IsType<SmartContractDoesNotExistException>(result.Exception);
        }

        [Fact]
        public void SME_CreateContract_ConstructorFails_Refund()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ContractConstructorInvalid.cs");

            var carrier = SmartContractCarrier.CreateContract(0, contractCode, 1, (Gas)10000);
            var tx = new Transaction();
            TxOut txOut = tx.AddOutput(0, new Script(carrier.Serialize()));
            var deserialized = SmartContractCarrier.Deserialize(tx, txOut);
            deserialized.Sender = new uint160(2);

            var executor = SmartContractExecutor.Initialize(deserialized, this.decompiler, this.gasInjector, this.network, this.state, this.validator, new Money(10000));
            ISmartContractExecutionResult result = executor.Execute(0, new uint160(1));

            Assert.NotNull(result.Exception);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void SME_CreateContract_MethodParameters_InvalidParameterCount()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ContractInvalidParameterCount.cs");

            string[] methodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Short, 5),
            };

            SmartContractCarrier carrier = SmartContractCarrier.CreateContract(0, contractCode, 1, (Gas)10000, methodParameters);
            var tx = new Transaction();
            TxOut txOut = tx.AddOutput(0, new Script(carrier.Serialize()));
            var deserialized = SmartContractCarrier.Deserialize(tx, txOut);
            deserialized.Sender = new uint160(2);

            var executor = SmartContractExecutor.Initialize(deserialized, this.decompiler, this.gasInjector, this.network, this.state, this.validator, new Money(10000));
            ISmartContractExecutionResult result = executor.Execute(0, new uint160(1));

            Assert.NotNull(result.Exception);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void SME_CreateContract_MethodParameters_ParameterTypeMismatch()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ContractMethodParameterTypeMismatch.cs");

            string[] methodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Bool, true),
            };

            var carrier = SmartContractCarrier.CreateContract(0, contractCode, 1, (Gas)10000, methodParameters);
            var tx = new Transaction();
            TxOut txOut = tx.AddOutput(0, new Script(carrier.Serialize()));
            var deserialized = SmartContractCarrier.Deserialize(tx, txOut);
            deserialized.Sender = new uint160(2);

            var executor = SmartContractExecutor.Initialize(deserialized, this.decompiler, this.gasInjector, this.network, this.state, this.validator, new Money(10000));
            ISmartContractExecutionResult result = executor.Execute(0, new uint160(1));

            Assert.NotNull(result.Exception);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }
    }
}