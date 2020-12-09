using System;
using System.IO;
using System.Linq;
using System.Threading;
using Mono.Cecil;
using Moq;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ObserverInstanceRewriterTests
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
        private readonly Address TestAddress;
        private ISmartContractState state;
        private const ulong Balance = 0;
        private const ulong GasLimit = 10000;
        private const ulong Value = 0;

        private readonly IILRewriter rewriter;
        private readonly IStateRepository repository;
        private readonly IContractModuleDefinitionReader moduleReader;
        private readonly ContractAssemblyLoader assemblyLoader;
        private readonly IGasMeter gasMeter;

        public ObserverInstanceRewriterTests()
        {
            var context = new ContractExecutorTestContext();
            this.TestAddress = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.repository = context.State;
            this.moduleReader = new ContractModuleDefinitionReader();
            this.assemblyLoader = new ContractAssemblyLoader();
            this.gasMeter = new GasMeter((Gas)5000000);

            var block = new TestBlock
            {
                Coinbase = this.TestAddress,
                Number = 1
            };
            var message = new TestMessage
            {
                ContractAddress = this.TestAddress,
                Sender = this.TestAddress,
                Value = Value
            };
            var getBalance = new Func<ulong>(() => Balance);
            var persistentState = new TestPersistentState();
            var network = new SmartContractsRegTest();
            var serializer = new ContractPrimitiveSerializer(network);
            this.state = new SmartContractState(
                new Block(1, this.TestAddress),
                new Message(this.TestAddress, this.TestAddress, 0),
                new PersistentState(new MeteredPersistenceStrategy(this.repository, this.gasMeter, new BasicKeyEncodingStrategy()),
                    context.Serializer, this.TestAddress.ToUint160()),
                context.Serializer,
                new ContractLogHolder(),
                Mock.Of<IInternalTransactionExecutor>(),
                new InternalHashHelper(),
                () => 1000,
                Mock.Of<IEcRecoverProvider>());

            this.rewriter = new ObserverInstanceRewriter();
        }

        [Fact]
        public void TestGasInjector()
        {
            ContractCompilationResult compilationResult = ContractCompiler.Compile(TestSource);
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(AppContext.BaseDirectory);

            using (ModuleDefinition moduleDefinition = ModuleDefinition.ReadModule(
                new MemoryStream(originalAssemblyBytes),
                new ReaderParameters { AssemblyResolver = resolver }))
            {
                TypeDefinition contractType = moduleDefinition.GetType(ContractName);
                MethodDefinition testMethod = contractType.Methods.FirstOrDefault(x => x.Name == MethodName);
            }

            var callData = new MethodCall("TestMethod", new object[] { 1 });

            IContractModuleDefinition module = this.moduleReader.Read(originalAssemblyBytes).Value;

            module.Rewrite(this.rewriter);

            CSharpFunctionalExtensions.Result<IContractAssembly> assembly = this.assemblyLoader.Load(module.ToByteCode());

            assembly.Value.SetObserver(new Observer(this.gasMeter, new MemoryMeter(ReflectionVirtualMachine.MemoryUnitLimit)));

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.Invoke(callData);
            // Number here shouldn't be hardcoded - note this is really only to let us know of consensus failure
            Assert.Equal(22uL, this.gasMeter.GasConsumed);
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

            // Because our contract contains an infinite loop, we want to kill our test after
            // some amount of time without achieving a result. 3 seconds is an arbitrarily high enough timeout
            // for the method body to have finished execution while minimising the amount of time we spend 
            // running tests
            // If you're running with the debugger on this will obviously be a source of failures
            IContractInvocationResult result = TimeoutHelper.RunCodeWithTimeout(5, () =>
            {
                // Need to set this here as the timeout helper will run this in a new thread and observer is thread static within the assembly.
                assembly.Value.SetObserver(new Observer(this.gasMeter, new MemoryMeter(ReflectionVirtualMachine.MemoryUnitLimit)));

                return contract.Invoke(callData);
            });

            Assert.False(result.IsSuccess);
            Assert.Equal((RuntimeObserver.Gas)0, this.gasMeter.GasAvailable);
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

            assembly.Value.SetObserver(new Observer(this.gasMeter, new MemoryMeter(ReflectionVirtualMachine.MemoryUnitLimit)));

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.InvokeConstructor(null);

            // Number here shouldn't be hardcoded - note this is really only to let us know of consensus failure
            Assert.Equal((RuntimeObserver.Gas)369, this.gasMeter.GasConsumed);
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

            assembly.Value.SetObserver(new Observer(this.gasMeter, new MemoryMeter(ReflectionVirtualMachine.MemoryUnitLimit)));

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.InvokeConstructor(new[] { "Test Owner" });

            // Number here shouldn't be hardcoded - note this is really only to let us know of consensus failure
            Assert.Equal((RuntimeObserver.Gas)328, this.gasMeter.GasConsumed);
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

            assembly.Value.SetObserver(new Observer(this.gasMeter, new MemoryMeter(ReflectionVirtualMachine.MemoryUnitLimit)));

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.Invoke(callData);

            Assert.True(result.IsSuccess);
            Assert.True(this.gasMeter.GasConsumed > 0);
        }

        [Fact]
        public void TestGasInjector_NestedType_GasInjectionSucceeds()
        {
            ContractCompilationResult compilationResult = ContractCompiler.Compile(@"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state): base(state) {
        var other = new Other.NestedOther();
        other.Loop();
    }
}

public static class Other
{
    public struct NestedOther {
        public void Loop() { while(true) {}}
    }
}
");
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            IContractModuleDefinition module = this.moduleReader.Read(originalAssemblyBytes).Value;

            module.Rewrite(this.rewriter);

            CSharpFunctionalExtensions.Result<IContractAssembly> assembly = this.assemblyLoader.Load(module.ToByteCode());

            assembly.Value.SetObserver(new Observer(this.gasMeter, new MemoryMeter(ReflectionVirtualMachine.MemoryUnitLimit)));

            IContract contract = Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);

            IContractInvocationResult result = contract.InvokeConstructor(null);

            Assert.False(result.IsSuccess);
            Assert.Equal(this.gasMeter.GasLimit, this.gasMeter.GasConsumed);
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

        [Fact]
        public void Separate_Threads_Have_Different_Observer_Instances()
        {
            // Create a contract assembly.
            ContractCompilationResult compilationResult = ContractCompiler.Compile(TestSource);
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            IContractModuleDefinition module = this.moduleReader.Read(originalAssemblyBytes).Value;

            module.Rewrite(this.rewriter);

            CSharpFunctionalExtensions.Result<IContractAssembly> assemblyLoadResult = this.assemblyLoader.Load(module.ToByteCode());

            IContractAssembly assembly = assemblyLoadResult.Value;

            var threadCount = 10;

            var observers = new Observer[threadCount];

            var countdown = new CountdownEvent(threadCount);

            var threads = new Thread[threadCount];

            for (var i = 0; i < threadCount; i++)
            {
                // Create a fake observer with a counter.
                var observer = new Observer(new GasMeter((Gas)1000000), new MemoryMeter(1000000));

                observers[i] = observer;

                // Set a different observer on each thread.
                // Invoke the same method on each thread.
                Thread newThread = new Thread(() =>
                {
                    assembly.SetObserver(observer);

                    Assert.Same(observer, assembly.GetObserver());
                    var callData = new MethodCall("TestMethod", new object[] { 1 });
                    IContract contract = Contract.CreateUninitialized(assembly.GetType(module.ContractType.Name), this.state, null);

                    IContractInvocationResult result = contract.Invoke(callData);

                    countdown.Signal();
                });

                newThread.Name = i.ToString();
                threads[i] = newThread;
            }

            // Start the threads on a separate loop to ensure all time is spent on processing.
            foreach (Thread thread in threads)
            {
                thread.Start();
            }

            countdown.Wait();

            foreach (Observer observer in observers)
            {
                Assert.Equal(observers[0].GasMeter.GasConsumed, observer.GasMeter.GasConsumed);
                Assert.Equal(observers[0].MemoryMeter.MemoryConsumed, observer.MemoryMeter.MemoryConsumed);
            }
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

            assembly.Value.SetObserver(new Observer(this.gasMeter, new MemoryMeter(ReflectionVirtualMachine.MemoryUnitLimit)));

            return Contract.CreateUninitialized(assembly.Value.GetType(module.ContractType.Name), this.state, null);
        }

    }
}