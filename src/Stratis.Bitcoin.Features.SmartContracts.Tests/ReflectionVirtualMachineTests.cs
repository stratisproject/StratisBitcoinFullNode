using System;
using System.IO;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Xunit;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class ReflectionVirtualMachineTests
    {
        private readonly SmartContractDecompiler decompiler;
        private readonly ISmartContractGasInjector gasInjector;
        private readonly Gas gasLimit;
        private readonly IGasMeter gasMeter;
        private readonly Network network;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly PersistentState persistentState;
        private readonly ContractStateRepositoryRoot state;

        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";

        public ReflectionVirtualMachineTests()
        {
            this.decompiler = new SmartContractDecompiler();
            this.gasInjector = new SmartContractGasInjector();
            this.network = Network.SmartContractsRegTest;
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.gasLimit = (Gas)10000;
            this.gasMeter = new GasMeter(this.gasLimit);

            this.state = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            var persistenceStrategy = new MeteredPersistenceStrategy(this.state, this.gasMeter, this.keyEncodingStrategy);
            this.persistentState = new PersistentState(this.state, persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
        }

        [Fact]
        public void VM_ExecuteContract_WithoutParameters()
        {
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageTest.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
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

                var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, this.keyEncodingStrategy);
                var persistentState = new PersistentState(repository, persistenceStrategy, deserializedCall.ContractAddress, this.network);

                var vm = new ReflectionVirtualMachine(persistentState);

                var sender = deserializedCall.Sender?.ToString() ?? TestAddress.ToString();

                var context = new SmartContractExecutionContext(
                                new Block(1, new Address("2")),
                                new Message(
                                    new Address(deserializedCall.ContractAddress.ToString()),
                                    new Address(sender),
                                    deserializedCall.TxOutValue,
                                    deserializedCall.GasLimit
                                    ),
                                deserializedCall.GasPrice
                            );

                var internalTransactionExecutor = new InternalTransactionExecutor(repository, this.network, this.keyEncodingStrategy);
                Func<ulong> getBalance = () => repository.GetCurrentBalance(deserializedCall.ContractAddress);


                ISmartContractExecutionResult result = vm.ExecuteMethod(
                    gasAwareExecutionCode,
                    "StorageTest",
                    "StoreData",
                    context,
                    gasMeter,
                    internalTransactionExecutor,
                    getBalance);

                stateRepository.Commit();

                Assert.Equal(Encoding.UTF8.GetBytes("TestValue"), stateRepository.GetStorageValue(deserializedCall.ContractAddress, Encoding.UTF8.GetBytes("TestKey")));
                Assert.Equal(Encoding.UTF8.GetBytes("TestValue"), repository.GetStorageValue(deserializedCall.ContractAddress, Encoding.UTF8.GetBytes("TestKey")));
            }
        }

        [Fact]
        public void VM_ExecuteContract_WithParameters()
        {
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageTestWithParameters.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
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

                var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, this.keyEncodingStrategy);
                var persistentState = new PersistentState(repository, persistenceStrategy, deserializedCall.ContractAddress, this.network);

                var vm = new ReflectionVirtualMachine(persistentState);
                var sender = deserializedCall.Sender?.ToString() ?? TestAddress;

                var context = new SmartContractExecutionContext(
                                new Block(1, new Address("2")),
                                new Message(
                                    deserializedCall.ContractAddress.ToAddress(this.network),
                                    new Address(sender),
                                    deserializedCall.TxOutValue,
                                    deserializedCall.GasLimit
                                    ),
                                deserializedCall.GasPrice,
                                deserializedCall.MethodParameters
                            );

                var internalTransactionExecutor = new InternalTransactionExecutor(repository, this.network, this.keyEncodingStrategy);
                Func<ulong> getBalance = () => repository.GetCurrentBalance(deserializedCall.ContractAddress);
                
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
        
        [Fact]
        public void VM_CreateContract_WithParameters()
        {
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.CompileFile("SmartContracts/SimpleAuction.cs");

            Assert.True(compilationResult.Success);
            byte[] contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------

            //    //Call smart contract and add to transaction-------------
            string[] methodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.ULong, 5),
            };

            var carrier = SmartContractCarrier.CreateContract(1, contractExecutionCode, 1, (Gas)500000, methodParameters);
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
                var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy());
                var persistentState = new PersistentState(repository, persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
                var vm = new ReflectionVirtualMachine(persistentState);
                var sender = deserializedCall.Sender?.ToString() ?? TestAddress;

                var context = new SmartContractExecutionContext(
                                new Block(1, TestAddress),
                                new Message(
                                    TestAddress,
                                    TestAddress,
                                    deserializedCall.TxOutValue,
                                    deserializedCall.GasLimit
                                    ),
                                deserializedCall.GasPrice,
                                deserializedCall.MethodParameters
                            );

                var internalTransactionExecutor = new InternalTransactionExecutor(repository, this.network, new BasicKeyEncodingStrategy());
                Func<ulong> getBalance = () => repository.GetCurrentBalance(deserializedCall.ContractAddress);

                ISmartContractExecutionResult result = vm.Create(
                    gasAwareExecutionCode,
                    "SimpleAuction",
                    context,
                    gasMeter,
                    internalTransactionExecutor,
                    getBalance);

                track.Commit();

                Assert.Equal(6, BitConverter.ToInt16(track.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("AuctionEndBlock")), 0));
                Assert.Equal(TestAddress.ToUint160(this.network).ToBytes(), track.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("Owner")));
            }
        }
    }
}