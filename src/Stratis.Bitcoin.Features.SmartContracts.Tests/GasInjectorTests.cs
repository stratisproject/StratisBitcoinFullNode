using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Stratis.SmartContracts.Executor.Reflection.Loader;
using Xunit;
using Block = Stratis.SmartContracts.Core.Block;
using InternalHashHelper = Stratis.SmartContracts.Core.Hashing.InternalHashHelper;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class GasInjectorTests
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

        private readonly Network network;
        private readonly IContractStateRoot repository;
        private readonly ReflectionVirtualMachine vm;
        private readonly SmartContractState state;
        private readonly GasMeter gasMeter;
        private readonly ContractAssemblyLoader assemblyLoader;
        private readonly ContractModuleDefinitionReader moduleReader;

        public GasInjectorTests()
        {
            // Only take from context what these tests require.
            var context = new ContractExecutorTestContext();
            this.vm = context.Vm;
            this.repository = context.State;
            this.network = context.Network;
            this.moduleReader = new ContractModuleDefinitionReader();
            this.assemblyLoader = new ContractAssemblyLoader();
            this.gasMeter = new GasMeter((Gas) 5000000);
            this.state = new SmartContractState(
                new Block(1, TestAddress),
                new Message(TestAddress, TestAddress, 0),
                new PersistentState(new MeteredPersistenceStrategy(this.repository, this.gasMeter, new BasicKeyEncodingStrategy()), 
                    context.ContractPrimitiveSerializer, TestAddress.ToUint160(this.network)),
                context.ContractPrimitiveSerializer,
                this.gasMeter,
                new ContractLogHolder(this.network),
                Mock.Of<IInternalTransactionExecutor>(),
                new InternalHashHelper(),
                () => 1000);
        }

        [Fact]
        public void TestGasInjector()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(TestSource);
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
           
            var module = this.moduleReader.Read(originalAssemblyBytes);
            module.InjectMethodGas("Test", callData);

            var assembly = this.assemblyLoader.Load(module.ToByteCode());

            var contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            var result = contract.Invoke(callData);

            Assert.Equal((ulong)aimGasAmount, this.state.GasMeter.GasConsumed);
        }

        [Fact]
        public void TestGasInjector_OutOfGasFails()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/OutOfGasTest.cs");
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var callData = new MethodCall("UseAllGas");

            var module = this.moduleReader.Read(originalAssemblyBytes);
            module.InjectMethodGas("OutOfGasTest", callData);

            var assembly = this.assemblyLoader.Load(module.ToByteCode());

            var contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            var result = contract.Invoke(callData);

            Assert.False(result.IsSuccess);
            Assert.Equal((Gas)0, this.gasMeter.GasAvailable);
            Assert.Equal(this.gasMeter.GasLimit, this.gasMeter.GasConsumed);
            Assert.Equal(this.gasMeter.GasLimit, this.gasMeter.GasConsumed);
        }

        [Fact]
        public void SmartContracts_GasInjector_SingleParamConstructorGasInjectedSuccess()
        {
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.Compile(TestSingleConstructorSource);

            Assert.True(compilationResult.Success);
            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var module = this.moduleReader.Read(originalAssemblyBytes);
            module.InjectConstructorGas();

            var assembly = this.assemblyLoader.Load(module.ToByteCode());

            var contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            var result = contract.InvokeConstructor(null);

            // TODO: Un-hard-code this. 
            // Constructor: 15
            // Property setter: 12
            // Storage: 150
            Assert.Equal((Gas)177, this.gasMeter.GasConsumed);
        }

        [Fact]
        public void SmartContracts_GasInjector_MultipleParamConstructorGasInjectedSuccess()
        {
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.Compile(TestMultipleConstructorSource);

            Assert.True(compilationResult.Success);
            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var module = this.moduleReader.Read(originalAssemblyBytes);
            module.InjectConstructorGas();

            var assembly = this.assemblyLoader.Load(module.ToByteCode());

            var contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            var result = contract.InvokeConstructor(new[] { "Test Owner" });

            // Constructor: 15
            // Property setter: 12
            // Storage: 150
            Assert.Equal((Gas)177, this.gasMeter.GasConsumed);
        }

        [Fact]
        public void TestGasInjector_ContractMethodWithRecursion_GasInjectionSucceeds()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/Recursion.cs");
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var callData = new MethodCall(nameof(Recursion.DoRecursion));

            var module = this.moduleReader.Read(originalAssemblyBytes);
            module.InjectMethodGas("Recursion", callData);

            var assembly = this.assemblyLoader.Load(module.ToByteCode());

            var contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);


            var result = contract.Invoke(callData);

            Assert.True(result.IsSuccess);
            Assert.True(this.gasMeter.GasConsumed > 0);
        }
    }
}