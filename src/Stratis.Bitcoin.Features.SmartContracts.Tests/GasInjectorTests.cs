﻿using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;
using Block = Stratis.SmartContracts.Core.Block;

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

        private readonly IKeyEncodingStrategy keyEncodingStrategy = BasicKeyEncodingStrategy.Default;

        private readonly ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));

        private readonly Network network = new SmartContractsRegTest();

        private readonly ILoggerFactory loggerFactory;

        public GasInjectorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
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

            var gasLimit = (Gas)500000;
            var gasMeter = new GasMeter(gasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy,
                TestAddress.ToUint160(this.network), this.network);
            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(internalTxExecutorFactory, this.loggerFactory);

            var executionContext = new SmartContractExecutionContext(
                new Block(0, TestAddress),
                new Message(TestAddress, TestAddress, 0, (Gas)500000), TestAddress.ToUint160(this.network), 1, new object[] { 1 });

            var result = vm.ExecuteMethod(
                originalAssemblyBytes,
                MethodName,
                executionContext,
                gasMeter, 
                persistentState, 
                this.repository);

            Assert.Equal(aimGasAmount, Convert.ToInt32(result.GasConsumed));
        }

        [Fact]
        public void TestGasInjector_OutOfGasFails()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/OutOfGasTest.cs");
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var gasLimit = (Gas)500000;
            var gasMeter = new GasMeter(gasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(internalTxExecutorFactory, this.loggerFactory);

            var executionContext = new SmartContractExecutionContext(new Block(0, TestAddress), new Message(TestAddress, TestAddress, 0, (Gas)500000), TestAddress.ToUint160(this.network), 1);

            var result = vm.ExecuteMethod(
                originalAssemblyBytes,
                "UseAllGas",
                executionContext,
                gasMeter, persistentState, repository);

            Assert.NotNull(result.ExecutionException);
            Assert.Equal((Gas)0, gasMeter.GasAvailable);
            Assert.Equal(gasLimit, result.GasConsumed);
            Assert.Equal(gasLimit, gasMeter.GasConsumed);
        }

        [Fact]
        public void SmartContracts_GasInjector_SingleParamConstructorGasInjectedSuccess()
        {
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.Compile(TestSingleConstructorSource);

            Assert.True(compilationResult.Success);
            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var gasLimit = (Gas)500000;
            var gasMeter = new GasMeter(gasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter, new BasicKeyEncodingStrategy());
            var persistentState = new PersistentState(persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(internalTxExecutorFactory, this.loggerFactory);

            var executionContext = new SmartContractExecutionContext(new Block(0, TestAddress), new Message(TestAddress, TestAddress, 0, (Gas)500000), TestAddress.ToUint160(this.network), 1);

            var result = vm.Create(
                originalAssemblyBytes,
                executionContext,
                gasMeter, persistentState, repository);

            // TODO: Un-hard-code this. 
            // Constructor: 15
            // Property setter: 12
            // Storage: 150
            Assert.Equal((Gas)177, result.GasConsumed);
        }

        [Fact]
        public void SmartContracts_GasInjector_MultipleParamConstructorGasInjectedSuccess()
        {
            SmartContractCompilationResult compilationResult =
                SmartContractCompiler.Compile(TestMultipleConstructorSource);

            Assert.True(compilationResult.Success);
            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var gasLimit = (Gas)500000;
            var gasMeter = new GasMeter(gasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter, new BasicKeyEncodingStrategy());
            var persistentState = new PersistentState(persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
            var internalTxExecutorFactory = new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(internalTxExecutorFactory, this.loggerFactory);
            var executionContext = new SmartContractExecutionContext(new Block(0, TestAddress), new Message(TestAddress, TestAddress, 0, (Gas)500000), TestAddress.ToUint160(this.network), 1, new[] { "Tset Owner" });

            var result = vm.Create(
                originalAssemblyBytes,
                executionContext,
                gasMeter, persistentState, repository);

            // Constructor: 15
            // Property setter: 12
            // Storage: 150
            Assert.Equal((Gas)177, result.GasConsumed);
        }
    }
}