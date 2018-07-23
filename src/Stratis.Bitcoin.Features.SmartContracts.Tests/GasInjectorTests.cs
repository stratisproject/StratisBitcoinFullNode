using System;
using System.Collections.Generic;
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
using Stratis.SmartContracts.Core.Validation;
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

        private readonly SmartContractValidator validator;

        public GasInjectorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.validator = new SmartContractValidator(new List<ISmartContractValidator>());
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
            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            
            var vm = new ReflectionVirtualMachine(this.validator, internalTxExecutorFactory, this.loggerFactory, this.network);

            var address = TestAddress.ToUint160(this.network);

            var callData = new CallData(gasLimit, address, "TestMethod", new object[] {1});

            var transactionContext = new TransactionContext(uint256.One, 0, address, address, 0);

            this.repository.SetCode(callData.ContractAddress, originalAssemblyBytes);
            this.repository.SetContractType(callData.ContractAddress, "Test");

            var result = vm.ExecuteMethod(gasMeter, 
                this.repository, 
                callData, 
                transactionContext);

            Assert.Equal(GasPriceList.BaseCost + (ulong) aimGasAmount, result.GasConsumed);
        }

        [Fact]
        public void TestGasInjector_OutOfGasFails()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/OutOfGasTest.cs");
            Assert.True(compilationResult.Success);

            byte[] originalAssemblyBytes = compilationResult.Compilation;

            var gasLimit = (Gas)500000;
            var gasMeter = new GasMeter(gasLimit);
            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(this.validator, internalTxExecutorFactory, this.loggerFactory, this.network);

            var address = TestAddress.ToUint160(this.network);

            var callData = new CallData(gasLimit, address, "UseAllGas");

            var transactionContext = new TransactionContext(uint256.One, 0, address, address, 0);

            this.repository.SetCode(callData.ContractAddress, originalAssemblyBytes);
            this.repository.SetContractType(callData.ContractAddress, "OutOfGasTest");

            var result = vm.ExecuteMethod(gasMeter, this.repository, callData, transactionContext);

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
            var internalTxExecutorFactory =
                new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(this.validator, internalTxExecutorFactory, this.loggerFactory, this.network);

            var callData = new CreateData(gasLimit, originalAssemblyBytes);
            
            var transactionContext = new TransactionContext(
                txHash: uint256.One,
                blockHeight: 0,
                coinbase: TestAddress.ToUint160(this.network),
                sender: TestAddress.ToUint160(this.network),
                amount: 0
            );

            var result = vm.Create(gasMeter, 
                this.repository,
                callData, 
                transactionContext);

            // TODO: Un-hard-code this. 
            // Basefee: 1000
            // Constructor: 15
            // Property setter: 12
            // Storage: 150
            Assert.Equal((Gas)1177, result.GasConsumed);
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
            var internalTxExecutorFactory = new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(this.validator, internalTxExecutorFactory, this.loggerFactory, this.network); 
            
            var callData = new CreateData(gasLimit, originalAssemblyBytes, new[] { "Test Owner" });

            var transactionContext = new TransactionContext(
                txHash: uint256.One,
                blockHeight: 0,
                coinbase: TestAddress.ToUint160(this.network),
                sender: TestAddress.ToUint160(this.network),
                amount: 0
            );

            var result = vm.Create(gasMeter, 
                this.repository, 
                callData, transactionContext);

            // Basefee: 1000
            // Constructor: 15
            // Property setter: 12
            // Storage: 150
            Assert.Equal((Gas)1177, result.GasConsumed);
        }
    }
}