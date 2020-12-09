using System;
using System.Reflection;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.SmartContracts.CLR.Caching;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public sealed class ReflectionVirtualMachineTests
    {
        private readonly Network network;
        private readonly ReflectionVirtualMachine vm;

        private readonly Address TestAddress;
        private IStateRepository state;
        private SmartContractState contractState;
        private ContractExecutorTestContext context;
        private readonly GasMeter gasMeter;

        public ReflectionVirtualMachineTests()
        {
            // Take what's needed for these tests
            this.context = new ContractExecutorTestContext();
            this.network = this.context.Network;
            this.TestAddress = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.vm = this.context.Vm;
            this.state = this.context.State;
            this.contractState = new SmartContractState(
                new Block(1, this.TestAddress),
                new Message(this.TestAddress, this.TestAddress, 0),
                new PersistentState(
                    new TestPersistenceStrategy(this.state),
                    this.context.Serializer, this.TestAddress.ToUint160()),
                this.context.Serializer,
                new ContractLogHolder(),
                Mock.Of<IInternalTransactionExecutor>(),
                new InternalHashHelper(),
                () => 1000,
                Mock.Of<IEcRecoverProvider>());
            this.gasMeter = new GasMeter((RuntimeObserver.Gas)50_000);
        }

        [Fact]
        public void VM_ExecuteContract_WithoutParameters()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageTest.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            byte[] codeHash = HashHelper.Keccak256(contractExecutionCode);

            var callData = new MethodCall("NoParamsTest");

            var executionContext = new ExecutionContext(new Observer(this.gasMeter, new MemoryMeter(100_000)));

            VmExecutionResult result = this.vm.ExecuteMethod(this.contractState,
                executionContext,
                callData,
                contractExecutionCode, "StorageTest");

            CachedAssemblyPackage cachedAssembly = this.context.ContractCache.Retrieve(new uint256(codeHash));

            // Check that it's been cached.
            Assert.NotNull(cachedAssembly);

            // Check that the observer has been reset.
            Assert.Null(cachedAssembly.Assembly.GetObserver());
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
            byte[] codeHash = HashHelper.Keccak256(contractExecutionCode);

            var methodParameters = new object[] { (int)5 };
            var callData = new MethodCall("OneParamTest", methodParameters);

            var executionContext = new ExecutionContext(new Observer(this.gasMeter, new MemoryMeter(100_000)));

            VmExecutionResult result = this.vm.ExecuteMethod(this.contractState,
                executionContext,
                callData,
                contractExecutionCode, "StorageTest");

            CachedAssemblyPackage cachedAssembly = this.context.ContractCache.Retrieve(new uint256(codeHash));

            // Check that it's been cached.
            Assert.NotNull(cachedAssembly);

            // Check that the observer has been reset.
            Assert.Null(cachedAssembly.Assembly.GetObserver());
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
            byte[] codeHash = HashHelper.Keccak256(contractExecutionCode);

            var methodParameters = new object[] { (ulong)5 };
            var executionContext = new ExecutionContext(new Observer(this.gasMeter, new MemoryMeter(100_000)));

            VmExecutionResult result = this.vm.Create(this.state, this.contractState, executionContext, contractExecutionCode, methodParameters);

            CachedAssemblyPackage cachedAssembly = this.context.ContractCache.Retrieve(new uint256(codeHash));

            // Check that it's been cached.
            Assert.NotNull(cachedAssembly);

            // Check that the observer has been reset.
            Assert.Null(cachedAssembly.Assembly.GetObserver());
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
            byte[] codeHash = HashHelper.Keccak256(contractExecutionCode);

            // Set up the state with an empty gasmeter so that out of gas occurs
            var contractState = Mock.Of<ISmartContractState>(s =>
                s.Block == Mock.Of<IBlock>(b => b.Number == 1 && b.Coinbase == this.TestAddress) &&
                s.Message == new Message(this.TestAddress, this.TestAddress, 0) &&
                s.PersistentState == new PersistentState(
                    new TestPersistenceStrategy(this.state),
                    this.context.Serializer, this.TestAddress.ToUint160()) &&
                s.Serializer == this.context.Serializer &&
                s.ContractLogger == new ContractLogHolder() &&
                s.InternalTransactionExecutor == Mock.Of<IInternalTransactionExecutor>() &&
                s.InternalHashHelper == new InternalHashHelper() &&
                s.GetBalance == new Func<ulong>(() => 0));

            var emptyGasMeter = new GasMeter((RuntimeObserver.Gas)0);
            var executionContext = new ExecutionContext(new Observer(emptyGasMeter, new MemoryMeter(100_000)));

            VmExecutionResult result = this.vm.Create(
                this.state,
                contractState,
                executionContext,
                contractExecutionCode,
                null);
            
            CachedAssemblyPackage cachedAssembly = this.context.ContractCache.Retrieve(new uint256(codeHash));

            // Check that it's been cached, even though we ran out of gas.
            Assert.NotNull(cachedAssembly);

            // Check that the observer has been reset.
            Assert.Null(cachedAssembly.Assembly.GetObserver());

            Assert.False(result.IsSuccess);
            Assert.Equal(VmExecutionErrorKind.OutOfGas, result.Error.ErrorKind);
        }

        [Fact]
        public void VM_ExecuteContract_ClearStorage()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ClearStorage.cs");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            byte[] codeHash = HashHelper.Keccak256(contractExecutionCode);

            var callData = new MethodCall(nameof(ClearStorage.ClearKey), new object[] { });

            uint160 contractAddress = this.contractState.Message.ContractAddress.ToUint160();
            byte[] keyToClear = Encoding.UTF8.GetBytes(ClearStorage.KeyToClear);

            // Set a value to be cleared
            this.state.SetStorageValue(contractAddress, keyToClear, new byte[] { 1, 2, 3 });

            var executionContext = new ExecutionContext(new Observer(this.gasMeter, new MemoryMeter(100_000)));

            VmExecutionResult result = this.vm.ExecuteMethod(this.contractState,
                executionContext,
                callData,
                contractExecutionCode,
                nameof(ClearStorage));

            CachedAssemblyPackage cachedAssembly = this.context.ContractCache.Retrieve(new uint256(codeHash));

            // Check that it's been cached for a successful call.
            Assert.NotNull(cachedAssembly);

            // Check that the observer has been reset.
            Assert.Null(cachedAssembly.Assembly.GetObserver());

            Assert.Null(result.Error);
            Assert.Null(this.state.GetStorageValue(contractAddress, keyToClear));
        }

        [Fact]
        public void VM_ExecuteContract_CachedAssembly_WithExistingObserver()
        {
            ContractCompilationResult compilationResult = ContractCompiler.Compile(
                @"
using System;
using Stratis.SmartContracts;

public class Contract : SmartContract
{
    public Contract(ISmartContractState state) : base(state) {}

    public void Test() {}
}
");
            Assert.True(compilationResult.Success);

            byte[] contractExecutionCode = compilationResult.Compilation;
            byte[] codeHash = HashHelper.Keccak256(contractExecutionCode);

            byte[] rewrittenCode;

            // Rewrite the assembly to have an observer.
            using (IContractModuleDefinition moduleDefinition = this.context.ModuleDefinitionReader.Read(contractExecutionCode).Value)
            {
                var rewriter = new ObserverInstanceRewriter();

                moduleDefinition.Rewrite(rewriter);

                rewrittenCode = moduleDefinition.ToByteCode().Value;
            }
            
            var contractAssembly = new ContractAssembly(Assembly.Load(rewrittenCode));

            // Cache the assembly.
            this.context.ContractCache.Store(new uint256(codeHash), new CachedAssemblyPackage(contractAssembly));

            // Set an observer on the cached rewritten assembly.
            var initialObserver = new Observer(new GasMeter((Gas)(this.gasMeter.GasAvailable + 1000)), new MemoryMeter(100_000));

            Assert.True(contractAssembly.SetObserver(initialObserver));
            
            var callData = new MethodCall("Test");

            // Run the execution with an empty gas meter, which means it should fail if the correct observer is used.
            var emptyGasMeter = new GasMeter((Gas)0);

            var executionContext = new ExecutionContext(new Observer(emptyGasMeter, new MemoryMeter(100_000)));

            VmExecutionResult result = this.vm.ExecuteMethod(this.contractState,
                executionContext,
                callData,
                contractExecutionCode,
                "Contract");

            CachedAssemblyPackage cachedAssembly = this.context.ContractCache.Retrieve(new uint256(codeHash));

            // Check that it's still cached.
            Assert.NotNull(cachedAssembly);

            // Check that the observer has been reset to the original.
            Assert.Same(initialObserver,cachedAssembly.Assembly.GetObserver());
            Assert.False(result.IsSuccess);
            Assert.Equal(VmExecutionErrorKind.OutOfGas, result.Error.ErrorKind);
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