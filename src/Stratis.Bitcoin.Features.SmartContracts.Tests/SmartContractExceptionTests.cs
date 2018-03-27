using System;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    /// <summary>
    /// These tests can be added to/removed from in future should we implement other exceptions
    /// which can possible affect how the caller gets refunded.
    /// </summary>
    public sealed class SmartContractExceptionTests
    {
        private readonly ContractStateRepositoryRoot repository;
        private readonly Network network;
        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";

        public SmartContractExceptionTests()
        {
            this.repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.network = Network.SmartContractsRegTest;
        }

        [Fact]
        public void VM_Throws_OutOfGasException_CanCatch()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowOutOfGasExceptionContract.cs");

            var gasLimit = (Gas)100;
            var gasMeter = new GasMeter(gasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter);
            var persistentState = new PersistentState(this.repository, persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Stratis.SmartContracts.Block(0, TestAddress),
                new Message(TestAddress, TestAddress, 0, gasLimit),
                1,
                new object[] { }
            );

            var internalTransactionExecutor = new InternalTransactionExecutor(this.repository, this.network);
            Func<ulong> getBalance = () => this.repository.GetCurrentBalance(TestAddress.ToUint160(this.network));

            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode,
                "ThrowOutOfGasExceptionContract",
                "ThrowException",
                context,
                gasMeter,
                internalTransactionExecutor,
                getBalance);

            Assert.Equal(typeof(OutOfGasException), result.Exception.GetType());
        }

        [Fact]
        public void VM_Throws_RefundGasException_CanCatch()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowRefundGasExceptionContract.cs");

            var gasLimit = (Gas)100;
            var gasMeter = new GasMeter(gasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter);
            var persistentState = new PersistentState(this.repository, persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Stratis.SmartContracts.Block(0, TestAddress),
                new Message(TestAddress, TestAddress, 0, gasLimit),
                1,
                new object[] { }
            );

            var internalTransactionExecutor = new InternalTransactionExecutor(this.repository, this.network);
            Func<ulong> getBalance = () => this.repository.GetCurrentBalance(TestAddress.ToUint160(this.network));

            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode,
                "ThrowRefundGasExceptionContract",
                "ThrowException",
                context,
                gasMeter,
                internalTransactionExecutor,
                getBalance);

            Assert.Equal<ulong>(10, result.GasConsumed);
            Assert.Equal(typeof(RefundGasException), result.Exception.GetType());
        }

        [Fact]
        public void VM_Throws_SystemException_CanCatch()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowSystemExceptionContract.cs");

            var gasLimit = (Gas)100;
            var gasMeter = new GasMeter(gasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter);
            var persistentState = new PersistentState(this.repository, persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Stratis.SmartContracts.Block(0, TestAddress),
                new Message(TestAddress, TestAddress, 0, (Gas)100),
                1,
                new object[] { }
            );

            var internalTransactionExecutor = new InternalTransactionExecutor(this.repository, this.network);
            Func<ulong> getBalance = () => this.repository.GetCurrentBalance(TestAddress.ToUint160(this.network));

            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode,
                "ThrowSystemExceptionContract",
                "ThrowException",
                context,
                gasMeter,
                internalTransactionExecutor,
                getBalance);

            Assert.Equal(typeof(Exception), result.Exception.GetType());
        }
    }
}