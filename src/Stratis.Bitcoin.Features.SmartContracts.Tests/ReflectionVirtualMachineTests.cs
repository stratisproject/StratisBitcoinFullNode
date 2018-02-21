using System;
using System.IO;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class ReflectionVirtualMachineTests
    {
        private readonly SmartContractDecompiler decompiler;
        private readonly SmartContractGasInjector gasInjector;

        public ReflectionVirtualMachineTests()
        {
            this.decompiler = new SmartContractDecompiler();
            this.gasInjector = new SmartContractGasInjector();
        }

        [Fact]
        public void VM_Execute_Contract_WithoutParameters()
        {
            //Get the contract execution code------------------------
            byte[] contractExecutionCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/StorageTest.cs");
            //-------------------------------------------------------

            //Call smart contract and add to transaction-------------
            SmartContractCarrier call = SmartContractCarrier.CallContract(1, new uint160(1), "StoreData", 1, (Gas) 500000);
            byte[] serializedCall = call.Serialize();
            var transactionCall = new Transaction();
            transactionCall.AddInput(new TxIn());
            TxOut callTxOut = transactionCall.AddOutput(0, new Script(serializedCall));
            //-------------------------------------------------------

            //Deserialize the contract from the transaction----------
            //and get the module definition
            var deserializedCall = SmartContractCarrier.Deserialize(transactionCall, callTxOut);
            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(contractExecutionCode); // Note that this is skipping validation and when on-chain, 
            //-------------------------------------------------------

            this.gasInjector.AddGasCalculationToContract(decompilation.ContractType, decompilation.BaseType);

            byte[] gasAwareExecutionCode;
            using (var ms = new MemoryStream())
            {
                decompilation.ModuleDefinition.Write(ms);
                gasAwareExecutionCode = ms.ToArray();

                var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
                IContractStateRepository track = repository.StartTracking();

                //@TODO Inject PersistentState or use factory method
                var persistentState = new PersistentState(repository, deserializedCall.To);
                var vm = new ReflectionVirtualMachine(persistentState);

                var context = new SmartContractExecutionContext(
                                new Stratis.SmartContracts.Block(1, new uint160(2), 0),
                                new Message(
                                    new Address(deserializedCall.To),
                                    new Address(deserializedCall.Sender),
                                    deserializedCall.TxOutValue,
                                    deserializedCall.GasLimit
                                    ),
                                deserializedCall.GasUnitPrice
                            );

                SmartContractExecutionResult result = vm.ExecuteMethod(gasAwareExecutionCode, "StorageTest", "StoreData", context);
                track.Commit();

                Assert.Equal(Encoding.UTF8.GetBytes("TestValue"), track.GetStorageValue(deserializedCall.To, Encoding.UTF8.GetBytes("TestKey")));
                Assert.Equal(Encoding.UTF8.GetBytes("TestValue"), repository.GetStorageValue(deserializedCall.To, Encoding.UTF8.GetBytes("TestKey")));
            }
        }

        [Fact]
        public void VM_Execute_Contract_WithParameters()
        {
            //Get the contract execution code------------------------
            byte[] contractExecutionCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/StorageTestWithParameters.cs");
            //-------------------------------------------------------

            //    //Call smart contract and add to transaction-------------
            string[] methodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Short, 5),
            };

            SmartContractCarrier call = SmartContractCarrier.CallContract(1, new uint160(1), "StoreData", 1, (Gas) 500000).WithParameters(methodParameters);
            byte[] serializedCall = call.Serialize();
            var transactionCall = new Transaction();
            transactionCall.AddInput(new TxIn());
            TxOut callTxOut = transactionCall.AddOutput(0, new Script(serializedCall));
            //-------------------------------------------------------

            //Deserialize the contract from the transaction----------
            //and get the module definition
            var deserializedCall = SmartContractCarrier.Deserialize(transactionCall, callTxOut);
            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(contractExecutionCode); // Note that this is skipping validation and when on-chain, 
            //-------------------------------------------------------

            this.gasInjector.AddGasCalculationToContract(decompilation.ContractType, decompilation.BaseType);

            byte[] gasAwareExecutionCode;
            using (var ms = new MemoryStream())
            {
                decompilation.ModuleDefinition.Write(ms);
                gasAwareExecutionCode = ms.ToArray();

                var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
                IContractStateRepository track = repository.StartTracking();

                //@TODO Inject PersistentState or use factory method
                var persistentState = new PersistentState(repository, deserializedCall.To);
                var vm = new ReflectionVirtualMachine(persistentState);

                var context = new SmartContractExecutionContext(
                                new Stratis.SmartContracts.Block(1, new uint160(2), 0),
                                new Message(
                                    new Address(deserializedCall.To),
                                    new Address(deserializedCall.Sender),
                                    deserializedCall.TxOutValue,
                                    deserializedCall.GasLimit
                                    ),
                                deserializedCall.GasUnitPrice,
                                deserializedCall.MethodParameters
                            );

                SmartContractExecutionResult result = vm.ExecuteMethod(gasAwareExecutionCode, "StorageTestWithParameters", "StoreData", context);
                track.Commit();

                Assert.Equal(5, BitConverter.ToInt16(track.GetStorageValue(context.Message.ContractAddress.ToUint160(), Encoding.UTF8.GetBytes("orders")), 0));
                Assert.Equal(5, BitConverter.ToInt16(repository.GetStorageValue(context.Message.ContractAddress.ToUint160(), Encoding.UTF8.GetBytes("orders")), 0));
            }
        }
    }
}