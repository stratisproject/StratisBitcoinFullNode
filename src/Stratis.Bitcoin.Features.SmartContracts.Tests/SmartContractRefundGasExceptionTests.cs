using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.Exceptions;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SmartContractRefundGasExceptionTests
    {
        private readonly ContractStateRepositoryRoot repository;

        public SmartContractRefundGasExceptionTests()
        {
            this.repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
        }

        [Fact]
        public void VM_Throws_SmartContractRefundGasException()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowRefundGasExceptionTest.cs");
            var persistentState = new PersistentState(this.repository, Address.Zero.ToUint160());
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Block(0, 0, 0),
                new Message(Address.Zero, Address.Zero, 0, 100),
                1,
                new object[] { }
            );

            SmartContractExecutionResult result = vm.ExecuteMethod(contractCode, "ThrowRefundGasExceptionTest", "ThrowException", context);
            Assert.Equal(typeof(SmartContractRefundGasException), result.RuntimeException.GetType());
        }
    }
}