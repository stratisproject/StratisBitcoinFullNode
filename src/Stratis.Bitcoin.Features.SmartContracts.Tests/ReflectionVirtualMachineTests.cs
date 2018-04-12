using System;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.State;
using Xunit;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class ReflectionVirtualMachineTests
    {
        private readonly Gas gasLimit;
        private readonly IGasMeter gasMeter;
        private readonly Network network;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly PersistentState persistentState;
        private readonly ContractStateRepositoryRoot state;

        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";

        public ReflectionVirtualMachineTests()
        {
            this.network = Network.SmartContractsRegTest;
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.gasLimit = (Gas)10000;
            this.gasMeter = new GasMeter(this.gasLimit);

            this.state = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            var persistenceStrategy = new MeteredPersistenceStrategy(this.state, this.gasMeter, this.keyEncodingStrategy);
            this.persistentState = new PersistentState(persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
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
            var deserializedCall = SmartContractCarrier.Deserialize(transactionCall, callTxOut);
            //-------------------------------------------------------

            var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            IContractStateRepository stateRepository = repository.StartTracking();

            var gasMeter = new GasMeter(deserializedCall.GasLimit);

            var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy, deserializedCall.ContractAddress, this.network);

            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.network, this.keyEncodingStrategy);
            var vm = new ReflectionVirtualMachine(persistentState, internalTxExecutorFactory, repository);

            var sender = deserializedCall.Sender?.ToString() ?? TestAddress.ToString();

            var context = new SmartContractExecutionContext(
                            new Block(1, new Address("2")),
                            new Message(
                                new Address(deserializedCall.ContractAddress.ToString()),
                                new Address(sender),
                                deserializedCall.TxOutValue,
                                deserializedCall.GasLimit
                                ),
                            TestAddress.ToUint160(this.network),
                            deserializedCall.GasPrice
                        );
            
            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractExecutionCode,
                "StoreData",
                context,
                gasMeter);

            stateRepository.Commit();

            Assert.Equal(Encoding.UTF8.GetBytes("TestValue"), stateRepository.GetStorageValue(deserializedCall.ContractAddress, Encoding.UTF8.GetBytes("TestKey")));
            Assert.Equal(Encoding.UTF8.GetBytes("TestValue"), repository.GetStorageValue(deserializedCall.ContractAddress, Encoding.UTF8.GetBytes("TestKey")));
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
            var deserializedCall = SmartContractCarrier.Deserialize(transactionCall, callTxOut);
            //-------------------------------------------------------

            var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            IContractStateRepository track = repository.StartTracking();

            var gasMeter = new GasMeter(deserializedCall.GasLimit);

            var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy, deserializedCall.ContractAddress, this.network);

            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.network, this.keyEncodingStrategy);
            var vm = new ReflectionVirtualMachine(persistentState, internalTxExecutorFactory, repository);
            
            var sender = deserializedCall.Sender?.ToString() ?? TestAddress;

            var context = new SmartContractExecutionContext(
                            new Block(1, new Address("2")),
                            new Message(
                                deserializedCall.ContractAddress.ToAddress(this.network),
                                new Address(sender),
                                deserializedCall.TxOutValue,
                                deserializedCall.GasLimit
                                ),
                            deserializedCall.ContractAddress,
                            deserializedCall.GasPrice,
                            deserializedCall.MethodParameters
                        );

            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractExecutionCode,
                "StoreData",
                context,
                gasMeter);

            track.Commit();

            Assert.Equal(5, BitConverter.ToInt16(track.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("orders")), 0));
            Assert.Equal(5, BitConverter.ToInt16(repository.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("orders")), 0));
        }

        [Fact]
        public void VM_CreateContract_WithParameters()
        {
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.CompileFile("SmartContracts/Auction.cs");

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
            var deserializedCall = SmartContractCarrier.Deserialize(transactionCall, callTxOut);
            //-------------------------------------------------------

            var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            IContractStateRepository track = repository.StartTracking();

            var gasMeter = new GasMeter(deserializedCall.GasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy());
            var persistentState = new PersistentState(persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.network, this.keyEncodingStrategy);
            var vm = new ReflectionVirtualMachine(persistentState, internalTxExecutorFactory, repository);

            var context = new SmartContractExecutionContext(
                            new Block(1, TestAddress),
                            new Message(
                                TestAddress,
                                TestAddress,
                                deserializedCall.TxOutValue,
                                deserializedCall.GasLimit
                                ),
                            TestAddress.ToUint160(this.network),
                            deserializedCall.GasPrice,
                            deserializedCall.MethodParameters
                        );
            
            ISmartContractExecutionResult result = vm.Create(
                contractExecutionCode,
                context,
                gasMeter);

            track.Commit();

            Assert.Equal(6, BitConverter.ToInt16(track.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("EndBlock")), 0));
            Assert.Equal(TestAddress.ToUint160(this.network).ToBytes(), track.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("Owner")));
        }
    }
}