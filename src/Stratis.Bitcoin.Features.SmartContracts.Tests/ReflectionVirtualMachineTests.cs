using System;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.ContractLogging;
using Xunit;
using Block = Stratis.SmartContracts.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class ReflectionVirtualMachineTests
    {
        private readonly Network network;
        private readonly ReflectionVirtualMachine vm;

        private readonly Address TestAddress;
        private IStateRepository state;
        private SmartContractState contractState;
        private ContractExecutorTestContext context;

        public ReflectionVirtualMachineTests()
        {
            // Take what's needed for these tests
            this.context = new ContractExecutorTestContext();
            this.network = context.Network;
            this.TestAddress = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.vm = context.Vm;
            this.state = context.State;
            this.contractState = new SmartContractState(
                new Block(1, TestAddress),
                new Message(TestAddress, TestAddress, 0),
                new PersistentState(
                    new TestPersistenceStrategy(this.state),
                    this.context.Serializer, TestAddress.ToUint160()),
                context.Serializer,
                new GasMeter((Gas)5000000),
                new ContractLogHolder(),
                Mock.Of<IInternalTransactionExecutor>(),
                new InternalHashHelper(),
                () => 1000);
        }

        [Fact]
        public void VM_ExecuteContract_WithoutParameters()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageTest.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;

            var callData = new MethodCall("NoParamsTest");

            VmExecutionResult result = this.vm.ExecuteMethod(this.contractState, 
                callData,
                contractExecutionCode, "StorageTest");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
            Assert.True((bool)result.Success.Result);
        }

        [Fact]
        public void VM_ExecuteContract_WithParameters()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageTest.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            var methodParameters = new object[] { (int)5 };
            var callData = new MethodCall("OneParamTest", methodParameters);
            
            VmExecutionResult result = this.vm.ExecuteMethod(this.contractState,
                callData,
                contractExecutionCode, "StorageTest");
            
            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
            Assert.Equal(methodParameters[0], result.Success.Result);
        }

        [Fact]
        public void VM_CreateContract_WithParameters()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Auction.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            var methodParameters = new object[] { (ulong)5 };

            VmExecutionResult result = this.vm.Create(this.state, this.contractState, contractExecutionCode, methodParameters);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
        }

        [Fact]
        public void VM_ExecuteContract_OutOfGas()
        {
            ContractCompilationResult compilationResult = ContractCompiler.Compile(
@"
using System;
using Stratis.SmartContracts;

public class Contract : SmartContract
{
    public Contract(ISmartContractState state) : base(state) {}
}
");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
    
            // Set up the state with an empty gasmeter so that out of gas occurs
            var contractState = Mock.Of<ISmartContractState>(s =>
                s.Block == Mock.Of<IBlock>(b => b.Number == 1 && b.Coinbase == TestAddress) &&
                s.Message == new Message(TestAddress, TestAddress, 0) &&
                s.PersistentState == new PersistentState(
                    new TestPersistenceStrategy(this.state),
                    this.context.Serializer, TestAddress.ToUint160()) &&
                s.Serializer == this.context.Serializer &&
                s.GasMeter == new GasMeter((Gas) 0) &&
                s.ContractLogger == new ContractLogHolder() &&
                s.InternalTransactionExecutor == Mock.Of<IInternalTransactionExecutor>() &&
                s.InternalHashHelper == new InternalHashHelper() &&
                s.GetBalance == new Func<ulong>(() => 0));

            VmExecutionResult result = this.vm.Create(this.state, contractState, contractExecutionCode, null);

            Assert.False(result.IsSuccess);
            Assert.Equal(VmExecutionErrorKind.OutOfGas, result.Error.ErrorKind);
        }

        [Fact]
        public void VM_ExecuteContract_ClearStorage()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ClearStorage.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            var callData = new MethodCall(nameof(ClearStorage.ClearKey), new object[] { });

            uint160 contractAddress = this.contractState.Message.ContractAddress.ToUint160();
            byte[] keyToClear = Encoding.UTF8.GetBytes(ClearStorage.KeyToClear);

            // Set a value to be cleared
            this.state.SetStorageValue(contractAddress, keyToClear, new byte[] { 1, 2, 3 });

            VmExecutionResult result = this.vm.ExecuteMethod(this.contractState,
                callData,
                contractExecutionCode, nameof(ClearStorage));

            Assert.Null(result.Error);
            Assert.Null(this.state.GetStorageValue(contractAddress, keyToClear));
        }
    }

    public class TestPersistenceStrategy : IPersistenceStrategy
    {
        private readonly IStateRepository stateDb;

        public TestPersistenceStrategy(IStateRepository stateDb)
        {
            this.stateDb = stateDb;
        }

        public bool ContractExists(uint160 address)
        {
            return this.stateDb.IsExist(address);
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            return this.stateDb.GetStorageValue(address, key);
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value)
        {
            this.stateDb.SetStorageValue(address, key, value);
        }
    }
}