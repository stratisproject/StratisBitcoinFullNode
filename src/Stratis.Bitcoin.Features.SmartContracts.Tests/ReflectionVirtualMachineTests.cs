using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

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
        private readonly SmartContractValidator validator;
        
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
            this.validator = new SmartContractValidator(new List<ISmartContractValidator>());
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
            var callData = new CallData((Gas)5000000, new uint160(1), "StoreData");
            var value = Money.Zero;
            //-------------------------------------------------------

            var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            IContractStateRepository stateRepository = repository.StartTracking();

            var gasMeter = new GasMeter(callData.GasLimit);

            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(this.validator, internalTxExecutorFactory, this.loggerFactory, this.network);

            var address = TestAddress.ToUint160(this.network);

            var transactionContext = new TransactionContext(uint256.One, 1, address, address, 0);

            repository.SetCode(callData.ContractAddress, contractExecutionCode);
            repository.SetContractType(callData.ContractAddress, "StorageTest");

            var result = vm.ExecuteMethod(gasMeter, 
                repository, 
                callData, 
                transactionContext);

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
            var callData = new CallData((Gas)5000000, new uint160(1), "StoreData", methodParameters);
            var value = Money.Zero;
            //-------------------------------------------------------

            var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            IContractStateRepository track = repository.StartTracking();

            var gasMeter = new GasMeter(callData.GasLimit);

            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(this.validator, internalTxExecutorFactory, this.loggerFactory, this.network);

            var address = TestAddress.ToUint160(this.network);

            var transactionContext = new TransactionContext(uint256.One, 1, address, address, value);

            repository.SetCode(callData.ContractAddress, contractExecutionCode);
            repository.SetContractType(callData.ContractAddress, "StorageTestWithParameters");

            var result = vm.ExecuteMethod(gasMeter, 
                repository, 
                callData, 
                transactionContext);

            track.Commit();

            Assert.Equal(5, BitConverter.ToInt16(track.GetStorageValue(callData.ContractAddress, Encoding.UTF8.GetBytes("orders")), 0));
            Assert.Equal(5, BitConverter.ToInt16(repository.GetStorageValue(callData.ContractAddress, Encoding.UTF8.GetBytes("orders")), 0));
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
            var callData = new CreateData((Gas)5000000, contractExecutionCode, methodParameters);
            var value = Money.Zero;
            //-------------------------------------------------------            

            var repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            IContractStateRepository track = repository.StartTracking();

            var gasMeter = new GasMeter(callData.GasLimit);

            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);

            var vm = new ReflectionVirtualMachine(this.validator, internalTxExecutorFactory, this.loggerFactory, this.network);

            var transactionContext = new TransactionContext(
                txHash: uint256.One,
                blockHeight: 1,
                coinbase: TestAddress.ToUint160(this.network),
                sender: TestAddress.ToUint160(this.network),
                amount: value
                );

            var result = vm.Create(gasMeter,
                repository, 
                callData,
                transactionContext);

            track.Commit();

            var endBlockValue = track.GetStorageValue(result.NewContractAddress,
                Encoding.UTF8.GetBytes("EndBlock"));
            Assert.Equal(6, BitConverter.ToInt16(endBlockValue, 0));
            Assert.Equal(TestAddress.ToUint160(this.network).ToBytes(), track.GetStorageValue(result.NewContractAddress, Encoding.UTF8.GetBytes("Owner")));
        }
    }
}