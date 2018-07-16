using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
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
        private readonly ILoggerFactory loggerFactory;
        private readonly PersistentState persistentState;
        private readonly ContractStateRepositoryRoot state;

        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";

        public ReflectionVirtualMachineTests()
        {
            this.network = new SmartContractsRegTest();
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
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

            //Set the calldata for the transaction----------
            var callData = new CallData(1, 1, (Gas)5000000, new uint160(1), "StoreData");
            var value = Money.Zero;
            //-------------------------------------------------------

            var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            IContractStateRepository stateRepository = repository.StartTracking();

            var gasMeter = new GasMeter(callData.GasLimit);

            var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy, callData.ContractAddress, this.network);

            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(internalTxExecutorFactory, this.loggerFactory);

            var sender = TestAddress.ToString();

            var context = new SmartContractExecutionContext(
                            new Block(1, new Address("2")),
                            new Message(
                                new Address(callData.ContractAddress.ToString()),
                                new Address(sender),
                                value,
                                callData.GasLimit
                                ),
                            TestAddress.ToUint160(this.network),
                            callData.GasPrice
                        );

            var result = vm.ExecuteMethod(
                contractExecutionCode,
                "StoreData",
                context,
                gasMeter, 
                persistentState, 
                repository);

            stateRepository.Commit();

            Assert.Equal(Encoding.UTF8.GetBytes("TestValue"), stateRepository.GetStorageValue(callData.ContractAddress, Encoding.UTF8.GetBytes("TestKey")));
            Assert.Equal(Encoding.UTF8.GetBytes("TestValue"), repository.GetStorageValue(callData.ContractAddress, Encoding.UTF8.GetBytes("TestKey")));
        }

        [Fact]
        public void VM_ExecuteContract_WithParameters()
        {
            //Get the contract execution code------------------------
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageTestWithParameters.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            //-------------------------------------------------------
            
            //Set the calldata for the transaction----------
            var methodParameters = new object[] { (short)5 };
            var callData = new CallData(1, 1, (Gas)5000000, new uint160(1), "StoreData", "", methodParameters);
            var value = Money.Zero;
            //-------------------------------------------------------

            var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            IContractStateRepository track = repository.StartTracking();

            var gasMeter = new GasMeter(callData.GasLimit);

            var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy, callData.ContractAddress, this.network);

            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(internalTxExecutorFactory, this.loggerFactory);

            var sender = TestAddress;

            var context = new SmartContractExecutionContext(
                            new Block(1, new Address("2")),
                            new Message(
                                callData.ContractAddress.ToAddress(this.network),
                                new Address(sender),
                                value,
                                callData.GasLimit
                                ),
                            callData.ContractAddress,
                            callData.GasPrice,
                            callData.MethodParameters
                        );

            var result = vm.ExecuteMethod(
                contractExecutionCode,
                "StoreData",
                context,
                gasMeter, 
                persistentState, 
                repository);

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

            //Set the calldata for the transaction----------
            var methodParameters = new object[] { (ulong)5 };
            var callData = new CallData(1, 1, (Gas)5000000, contractExecutionCode, "", methodParameters);
            var value = Money.Zero;
            //-------------------------------------------------------            

            var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            IContractStateRepository track = repository.StartTracking();

            var gasMeter = new GasMeter(callData.GasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy());
            var persistentState = new PersistentState(persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(internalTxExecutorFactory, this.loggerFactory);

            var context = new SmartContractExecutionContext(
                            new Block(1, TestAddress),
                            new Message(
                                TestAddress,
                                TestAddress,
                                value,
                                callData.GasLimit
                                ),
                            TestAddress.ToUint160(this.network),
                            callData.GasPrice,
                            callData.MethodParameters
                        );

            var result = vm.Create(
                contractExecutionCode,
                context,
                gasMeter, 
                persistentState,
                repository);

            track.Commit();

            Assert.Equal(6, BitConverter.ToInt16(track.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("EndBlock")), 0));
            Assert.Equal(TestAddress.ToUint160(this.network).ToBytes(), track.GetStorageValue(context.Message.ContractAddress.ToUint160(this.network), Encoding.UTF8.GetBytes("Owner")));
        }
    }
}