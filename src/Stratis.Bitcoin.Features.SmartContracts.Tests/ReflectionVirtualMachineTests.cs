using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Xunit;
using Block = Stratis.SmartContracts.Core.Block;
using InternalHashHelper = Stratis.SmartContracts.Core.Hashing.InternalHashHelper;
using Message = Stratis.SmartContracts.Core.Message;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class ReflectionVirtualMachineTests
    {
        private readonly Network network;
        private readonly ReflectionVirtualMachine vm;

        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";
        private IContractState state;
        private SmartContractState contractState;

        public ReflectionVirtualMachineTests()
        {
            // Take what's needed for these tests
            var context = new ContractExecutorTestContext();
            this.network = context.Network;
            this.vm = context.Vm;
            this.state = context.State;
            this.contractState = new SmartContractState(
                new Block(1, TestAddress),
                new Message(TestAddress, TestAddress, 0),
                new PersistentState(new PersistenceStrategy(this.state),
                    context.ContractPrimitiveSerializer, TestAddress.ToUint160(this.network)),
                context.ContractPrimitiveSerializer,
                new GasMeter((Gas)5000000),
                new ContractLogHolder(this.network),
                Mock.Of<IInternalTransactionExecutor>(),
                new InternalHashHelper(),
                () => 1000);
        }

        [Fact]
        public void VM_ExecuteContract_WithoutParameters()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageTest.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;

            var callData = new MethodCall("NoParamsTest");

            VmExecutionResult result = this.vm.ExecuteMethod(this.contractState, 
                callData,
                contractExecutionCode, "StorageTest");

            Assert.Null(result.ExecutionException);
            Assert.True((bool)result.Result);
        }

        [Fact]
        public void VM_ExecuteContract_WithParameters()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageTest.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            var methodParameters = new object[] { (int)5 };
            var callData = new MethodCall("OneParamTest", methodParameters);
            
            VmExecutionResult result = this.vm.ExecuteMethod(this.contractState,
                callData,
                contractExecutionCode, "StorageTest");

            Assert.Null(result.ExecutionException);
            Assert.Equal(methodParameters[0], result.Result);
        }

        [Fact]
        public void VM_CreateContract_WithParameters()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/Auction.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            var methodParameters = new object[] { (ulong)5 };

            VmExecutionResult result = this.vm.Create(this.state, this.contractState, contractExecutionCode, methodParameters);

            Assert.Null(result.ExecutionException);
        }
    }
}