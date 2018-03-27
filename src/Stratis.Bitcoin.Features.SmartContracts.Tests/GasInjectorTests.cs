using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Xunit;

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

        private const string ContractName = "Test";
        private const string MethodName = "TestMethod";
        private static readonly Address TestAddress =  (Address) "mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";

        private readonly ISmartContractGasInjector spendGasInjector = new SmartContractGasInjector();

        private readonly ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));

        private readonly Network network = Network.SmartContractsRegTest;

        // TODO: Right now the gas injector is only taking into account the instructions
        // in the user-defined methods. Calls to System methods aren't increasing the instructions.
        // Need to work this out somehow. Averages?

        // ALSO, write tests to check the different branches of code

        [Fact]
        public void TestGasInjector()
        {
            byte[] originalAssemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(TestSource);

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(AppContext.BaseDirectory);
            ModuleDefinition moduleDefinition = ModuleDefinition.ReadModule(new MemoryStream(originalAssemblyBytes), new ReaderParameters { AssemblyResolver = resolver });
            TypeDefinition contractType = moduleDefinition.GetType(ContractName);
            TypeDefinition baseType = contractType.BaseType.Resolve();
            MethodDefinition testMethod = contractType.Methods.FirstOrDefault(x => x.Name == MethodName);
            MethodDefinition constructorMethod = contractType.Methods.FirstOrDefault(x => x.Name.Contains("ctor"));
            int aimGasAmount = testMethod.Body.Instructions.Count; // + constructorMethod.Body.Instructions.Count; // Have to figure out ctor gas metering

            this.spendGasInjector.AddGasCalculationToContract(contractType, baseType);

            using (var mem = new MemoryStream())
            {
                moduleDefinition.Write(mem);
                byte[] injectedAssemblyBytes = mem.ToArray();

                var gasLimit = (Gas)500000;
                var gasMeter = new GasMeter(gasLimit);
                var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter);
                var persistentState = new PersistentState(this.repository, persistenceStrategy, TestAddress.ToUint160(this.network), this.network);
                var vm = new ReflectionVirtualMachine(persistentState);

                var executionContext = new SmartContractExecutionContext(new Stratis.SmartContracts.Block(0, TestAddress), new Message(TestAddress, TestAddress, 0, (Gas) 500000), 1, new object[] { 1 });

                var internalTransactionExecutor = new InternalTransactionExecutor(this.repository, this.network);
                Func<ulong> getBalance = () => repository.GetCurrentBalance(TestAddress.ToUint160(this.network));

                ISmartContractExecutionResult result = vm.ExecuteMethod(
                    injectedAssemblyBytes, 
                    ContractName, 
                    MethodName, 
                    executionContext,
                    gasMeter,
                    internalTransactionExecutor,
                    getBalance);
                Assert.Equal(aimGasAmount, Convert.ToInt32(result.GasConsumed));
            }
        }
    }
}