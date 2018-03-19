using System;
using System.IO;
using System.Text;
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
    public sealed class ReflectionVirtualMachineTests
    {
        private readonly SmartContractDecompiler decompiler;
        private readonly ISmartContractGasInjector gasInjector;
        private readonly Network network;

        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";

        public ReflectionVirtualMachineTests()
        {
            this.decompiler = new SmartContractDecompiler();
            this.gasInjector = new SmartContractGasInjector();
            this.network = Network.SmartContractsRegTest;
        }

        [Fact]
        public void VM_Execute_Contract_WithoutParameters()
        {
            //Get the contract execution code------------------------
            byte[] contractExecutionCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/StorageTest.cs");
            //-------------------------------------------------------

            //Call smart contract and add to transaction-------------
            var carrier = SmartContractCarrier.CallContract(1, new uint160(1), "StoreData", 1, (Gas)500000);
            var transactionCall = new Transaction();
            transactionCall.AddInput(new TxIn());
            TxOut callTxOut = transactionCall.AddOutput(0, new Script(carrier.Serialize()));
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
                IContractStateRepository stateRepository = repository.StartTracking();

                var gasMeter = new GasMeter(deserializedCall.GasLimit);
                var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter);
                var persistentState = new PersistentState(repository, persistenceStrategy, deserializedCall.To, this.network);
                var vm = new ReflectionVirtualMachine(persistentState);

                var sender = deserializedCall.Sender?.ToString() ?? TestAddress.ToString();

                var context = new SmartContractExecutionContext(
                                new Stratis.SmartContracts.Block(1, new Address("2"), 0),
                                new Message(
                                    new Address(deserializedCall.To.ToString()),
                                    new Address(sender),
                                    deserializedCall.TxOutValue,
                                    deserializedCall.GasLimit
                                    ),
                                deserializedCall.GasUnitPrice
                            );

                var internalTransactionExecutor = new InternalTransactionExecutor(repository, this.network);
                Func<ulong> getBalance = () => repository.GetCurrentBalance(deserializedCall.To);

                ISmartContractExecutionResult result = vm.ExecuteMethod(
                    gasAwareExecutionCode,
                    "StorageTest",
                    "StoreData",
                    context,
                    gasMeter,
                    internalTransactionExecutor,
                    getBalance);

                stateRepository.Commit();

                Assert.Equal(Encoding.UTF8.GetBytes("TestValue"), stateRepository.GetStorageValue(deserializedCall.To, Encoding.UTF8.GetBytes("TestKey")));
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

            var carrier = SmartContractCarrier.CallContract(1, new uint160(1), "StoreData", 1, (Gas)500000, methodParameters);
            var transactionCall = new Transaction();
            transactionCall.AddInput(new TxIn());
            TxOut callTxOut = transactionCall.AddOutput(0, new Script(carrier.Serialize()));
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

                var gasMeter = new GasMeter(deserializedCall.GasLimit);
                var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter);
                var persistentState = new PersistentState(repository, persistenceStrategy, deserializedCall.To, this.network);
                var vm = new ReflectionVirtualMachine(persistentState);
                var sender = deserializedCall.Sender?.ToString() ?? TestAddress;

                var context = new SmartContractExecutionContext(
                                new Stratis.SmartContracts.Block(1, new Address("2"), 0),
                                new Message(
                                    deserializedCall.To.ToAddress(this.network),
                                    new Address(sender),
                                    deserializedCall.TxOutValue,
                                    deserializedCall.GasLimit
                                    ),
                                deserializedCall.GasUnitPrice,
                                deserializedCall.MethodParameters
                            );


                var internalTransactionExecutor = new InternalTransactionExecutor(repository, this.network);
                Func<ulong> getBalance = () => repository.GetCurrentBalance(deserializedCall.To);
                
                ISmartContractExecutionResult result = vm.ExecuteMethod(
                    gasAwareExecutionCode,
                    "StorageTestWithParameters",
                    "StoreData",
                    context,
                    gasMeter,
                    internalTransactionExecutor,
                    getBalance);

                track.Commit();

                Assert.Equal(5, BitConverter.ToInt16(track.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("orders")), 0));
                Assert.Equal(5, BitConverter.ToInt16(repository.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("orders")), 0));
            }
        }
    }
}