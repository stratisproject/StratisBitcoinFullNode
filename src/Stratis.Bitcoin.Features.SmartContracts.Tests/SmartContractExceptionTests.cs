using System;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.Exceptions;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class SmartContractExceptionTests
    {
        private readonly ContractStateRepositoryRoot repository;

        public SmartContractExceptionTests()
        {
            this.repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
        }

        [Fact]
        public void VM_Throws_CanCatch_SystemException()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowSystemExceptionContract.cs");
            var persistentState = new PersistentState(this.repository, Address.Zero.ToUint160());
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Block(0, 0, 0),
                new Message(Address.Zero, Address.Zero, 0, 100),
                1,
                new object[] { }
            );

            SmartContractExecutionResult result = vm.ExecuteMethod(contractCode, "ThrowSystemExceptionContract", "ThrowException", context);
            Assert.Equal(typeof(Exception), result.Exception.GetType());
        }

        [Fact]
        public void VM_Throws_CanCatch_SmartContractRefundGasException()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowRefundGasExceptionContract.cs");
            var persistentState = new PersistentState(this.repository, Address.Zero.ToUint160());
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Block(0, 0, 0),
                new Message(Address.Zero, Address.Zero, 0, 100),
                1,
                new object[] { }
            );

            SmartContractExecutionResult result = vm.ExecuteMethod(contractCode, "ThrowRefundGasExceptionContract", "ThrowException", context);
            Assert.Equal(typeof(RefundGasException), result.Exception.GetType());
        }
    }
}