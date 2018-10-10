using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Moq;
using NBitcoin;
using RuntimeObserver;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Stratis.SmartContracts.Executor.Reflection.ILRewrite;
using Stratis.SmartContracts.Executor.Reflection.Loader;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ObserverTests
    {
        private const string TestSource = @"using System;
                                            using Stratis.SmartContracts;   

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state) : base(state) {}

                                                public void TestMethod(int number)
                                                {
                                                    int test = 11 + number;
                                                    var things = new string[]{""Something"", ""SomethingElse""};
                                                    test += things.Length;
                                                }
                                            }";

        private const string TestSingleConstructorSource = @"using System;
                                            using Stratis.SmartContracts;   

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state) : base(state) 
                                                {
                                                    this.Owner = ""Test Owner"";
                                                    string newString = this.Owner + 1;
                                                }

                                                public void TestMethod(int number)
                                                {
                                                    int test = 11 + number;
                                                    var things = new string[]{""Something"", ""SomethingElse""};
                                                    test += things.Length;
                                                }

                                                public string Owner {
                                                    get => this.PersistentState.GetString(""Owner"");
                                                    set => this.PersistentState.SetString(""Owner"", value);
                                                }
                                            }";

        private const string TestMultipleConstructorSource = @"using System;
                                            using Stratis.SmartContracts;   

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state, string ownerName) : base(state) 
                                                {
                                                    this.Owner = ownerName;
                                                }

                                                public void TestMethod(int number)
                                                {
                                                    int test = 11 + number;
                                                    var things = new string[]{""Something"", ""SomethingElse""};
                                                    test += things.Length;
                                                }

                                                public string Owner {
                                                    get => this.PersistentState.GetString(""Owner"");
                                                    set => this.PersistentState.SetString(""Owner"", value);
                                                }
                                            }";

        private const string ContractName = "Test";
        private const string MethodName = "TestMethod";
        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";
        private ISmartContractState state;
        private const ulong Balance = 0;
        private const ulong GasLimit = 10000;
        private const ulong Value = 0;

        private readonly ObserverRewriter rewriter;
        private readonly IStateRepository repository;
        private readonly Network network;
        private readonly IContractModuleDefinitionReader moduleReader;
        private readonly ContractAssemblyLoader assemblyLoader;
        private readonly IGasMeter gasMeter;

        public ObserverTests()
        {
            var context = new ContractExecutorTestContext();
            this.repository = context.State;
            this.network = context.Network;
            this.moduleReader = new ContractModuleDefinitionReader();
            this.assemblyLoader = new ContractAssemblyLoader();
            this.gasMeter = new GasMeter((Gas)5000000);

            var block = new TestBlock
            {
                Coinbase = TestAddress,
                Number = 1
            };
            var message = new TestMessage  
            {
                ContractAddress = TestAddress,
                GasLimit = (Gas)GasLimit,
                Sender = TestAddress,
                Value = Value
            };
            var getBalance = new Func<ulong>(() => Balance);
            var persistentState = new TestPersistentState();
            var network = new SmartContractsRegTest();
            var serializer = new ContractPrimitiveSerializer(network);
            this.state = new SmartContractState(
                new Stratis.SmartContracts.Core.Block(1, TestAddress),
                new Message(TestAddress, TestAddress, 0),
                new PersistentState(new MeteredPersistenceStrategy(this.repository, this.gasMeter, new BasicKeyEncodingStrategy()),
                    context.ContractPrimitiveSerializer, TestAddress.ToUint160(this.network)),
                context.ContractPrimitiveSerializer,
                this.gasMeter,
                new ContractLogHolder(this.network),
                Mock.Of<IInternalTransactionExecutor>(),
                new InternalHashHelper(),
                () => 1000);

            this.rewriter = new ObserverRewriter(new Observer(this.gasMeter, ReflectionVirtualMachine.MemoryUnitLimit));
        }

        [Fact]
        public void TestGasInjector()
        {
            ContractCompilationResult compilationResult = ContractCompiler.Compile(TestSource);
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(AppContext.BaseDirectory);
            int aimGasAmount;

            using (ModuleDefinition moduleDefinition = ModuleDefinition.ReadModule(
                new MemoryStream(originalAssemblyBytes),
                new ReaderParameters { AssemblyResolver = resolver }))
            {
                TypeDefinition contractType = moduleDefinition.GetType(ContractName);
                MethodDefinition testMethod = contractType.Methods.FirstOrDefault(x => x.Name == MethodName);
                aimGasAmount =
                    testMethod?.Body?.Instructions?
                        .Count ?? 10000000;
            }

            var callData = new MethodCall("TestMethod", new object[] { 1 });

            IContractModuleDefinition module = this.moduleReader.Read(originalAssemblyBytes).Value;
            
            module.Rewrite(this.rewriter);

            CSharpFunctionalExtensions.Result<IContractAssembly> assembly = this.assemblyLoader.Load(module.ToByteCode());

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.Invoke(callData);

            Assert.Equal((ulong)aimGasAmount, this.state.GasMeter.GasConsumed);
        }

        [Fact]
        public void TestGasInjector_OutOfGasFails()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/OutOfGasTest.cs");
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var callData = new MethodCall("UseAllGas");

            IContractModuleDefinition module = this.moduleReader.Read(originalAssemblyBytes).Value;

            module.Rewrite(this.rewriter);

            CSharpFunctionalExtensions.Result<IContractAssembly> assembly = this.assemblyLoader.Load(module.ToByteCode());

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.Invoke(callData);

            Assert.False(result.IsSuccess);
            Assert.Equal((Gas)0, this.gasMeter.GasAvailable);
            Assert.Equal(this.gasMeter.GasLimit, this.gasMeter.GasConsumed);
            Assert.Equal(this.gasMeter.GasLimit, this.gasMeter.GasConsumed);
        }

        [Fact]
        public void SmartContracts_GasInjector_SingleParamConstructorGasInjectedSuccess()
        {
            ContractCompilationResult compilationResult =
                ContractCompiler.Compile(TestSingleConstructorSource);

            Assert.True(compilationResult.Success);
            byte[] originalAssemblyBytes = compilationResult.Compilation;

            IContractModuleDefinition module = this.moduleReader.Read(originalAssemblyBytes).Value;

            module.Rewrite(this.rewriter);

            CSharpFunctionalExtensions.Result<IContractAssembly> assembly = this.assemblyLoader.Load(module.ToByteCode());

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.InvokeConstructor(null);

            // TODO: Un-hard-code this. 
            // Constructor: 15
            // Property setter: 22
            // Storage: 150
            // "string newString = this.Owner + 1;": 36
            Assert.Equal((Gas)223, this.gasMeter.GasConsumed);
        }

        [Fact]
        public void SmartContracts_GasInjector_MultipleParamConstructorGasInjectedSuccess()
        {
            ContractCompilationResult compilationResult =
                ContractCompiler.Compile(TestMultipleConstructorSource);

            Assert.True(compilationResult.Success);
            byte[] originalAssemblyBytes = compilationResult.Compilation;

            IContractModuleDefinition module = this.moduleReader.Read(originalAssemblyBytes).Value;

            module.Rewrite(this.rewriter);

            CSharpFunctionalExtensions.Result<IContractAssembly> assembly = this.assemblyLoader.Load(module.ToByteCode());

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.InvokeConstructor(new[] { "Test Owner" });

            // Constructor: 15
            // Property setter: 17
            // Storage: 150
            Assert.Equal((Gas)182, this.gasMeter.GasConsumed);
        }

        [Fact]
        public void TestGasInjector_ContractMethodWithRecursion_GasInjectionSucceeds()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Recursion.cs");
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var callData = new MethodCall(nameof(Recursion.DoRecursion));

            IContractModuleDefinition module = this.moduleReader.Read(originalAssemblyBytes).Value;

            module.Rewrite(this.rewriter);

            CSharpFunctionalExtensions.Result<IContractAssembly> assembly = this.assemblyLoader.Load(module.ToByteCode());

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.Invoke(callData);

            Assert.True(result.IsSuccess);
            Assert.True(this.gasMeter.GasConsumed > 0);
        }


        [Fact]
        public void Test_MemoryLimit_Small_Allocations_Pass()
        {
            IContract contract = GetContractAfterRewrite("SmartContracts/MemoryLimit.cs");

            // Small array passes
            AssertPasses(contract, nameof(MemoryLimit.AllowedArray));

            // Small array resize passes
            AssertPasses(contract, nameof(MemoryLimit.AllowedArrayResize));

            // Small string constructor passes
            AssertPasses(contract, nameof(MemoryLimit.AllowedStringConstructor));

            // Small ToCharArray passes
            AssertPasses(contract, nameof(MemoryLimit.AllowedToCharArray));

            // Small Split passes
            AssertPasses(contract, nameof(MemoryLimit.AllowedSplit));

            // Small Join passes
            AssertPasses(contract, nameof(MemoryLimit.AllowedJoin));

            // Small Concat passes
            AssertPasses(contract, nameof(MemoryLimit.AllowedConcat));
        }

        // These are all split up because if they all use the same one
        // they will have the same 'Observer' and it will overflow.
        // TODO: Future improvement: Use Theory, and don't compile from scratch
        // every time to save performance.

        [Fact]
        public void Test_MemoryLimit_BigArray_Fails()
        {
            IContract contract = GetContractAfterRewrite("SmartContracts/MemoryLimit.cs");
            AssertFailsDueToMemory(contract, nameof(MemoryLimit.NotAllowedArray));
        }

        [Fact]
        public void Test_MemoryLimit_BigArrayResize_Fails()
        {
            IContract contract = GetContractAfterRewrite("SmartContracts/MemoryLimit.cs");
            AssertFailsDueToMemory(contract, nameof(MemoryLimit.NotAllowedArrayResize));
        }

        [Fact]
        public void Test_MemoryLimit_BigStringConstructor_Fails()
        {
            IContract contract = GetContractAfterRewrite("SmartContracts/MemoryLimit.cs");
            AssertFailsDueToMemory(contract, nameof(MemoryLimit.NotAllowedStringConstructor));
        }

        [Fact]
        public void Test_MemoryLimit_BigCharArray_Fails()
        {
            IContract contract = GetContractAfterRewrite("SmartContracts/MemoryLimit.cs");
            AssertFailsDueToMemory(contract, nameof(MemoryLimit.NotAllowedToCharArray));
        }

        [Fact]
        public void Test_MemoryLimit_BigStringSplit_Fails()
        {
            IContract contract = GetContractAfterRewrite("SmartContracts/MemoryLimit.cs");
            AssertFailsDueToMemory(contract, nameof(MemoryLimit.NotAllowedSplit));
        }

        [Fact]
        public void Test_MemoryLimit_BigStringJoin_Fails()
        {
            IContract contract = GetContractAfterRewrite("SmartContracts/MemoryLimit.cs");
            AssertFailsDueToMemory(contract, nameof(MemoryLimit.NotAllowedJoin));
        }

        [Fact]
        public void Test_MemoryLimit_BigStringConcat_Fails()
        {
            IContract contract = GetContractAfterRewrite("SmartContracts/MemoryLimit.cs");
            AssertFailsDueToMemory(contract, nameof(MemoryLimit.NotAllowedConcat));
        }

        private void AssertFailsDueToMemory(IContract contract, string methodName)
        {
            var callData = new MethodCall(methodName);
            IContractInvocationResult result = contract.Invoke(callData);
            Assert.False(result.IsSuccess);
            Assert.Equal(ContractInvocationErrorType.OverMemoryLimit, result.InvocationErrorType);
        }

        private void AssertPasses(IContract contract, string methodName)
        {
            var callData = new MethodCall(methodName);
            IContractInvocationResult result = contract.Invoke(callData);
            Assert.True(result.IsSuccess);
        }

        private IContract GetContractAfterRewrite(string filename)
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile(filename);
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            IContractModuleDefinition module = this.moduleReader.Read(originalAssemblyBytes).Value;

            module.Rewrite(this.rewriter);

            CSharpFunctionalExtensions.Result<IContractAssembly> assembly = this.assemblyLoader.Load(module.ToByteCode());

            return Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);
        }

    }
}
