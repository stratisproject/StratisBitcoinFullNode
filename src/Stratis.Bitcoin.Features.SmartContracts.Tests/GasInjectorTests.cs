using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class GasInjectorTests
    {
        private const string TestSource = @"using System;
                                            using Stratis.SmartContracts;   

                                            public class Test : SmartContract
                                            {
                                                public Test(SmartContractState state) : base(state) {}

                                                public void TestMethod(int number)
                                                {
                                                    int test = 11 + number;
                                                    var things = new string[]{""Something"", ""SomethingElse""};
                                                    test += things.Length;
                                                }
                                            }";

        private const string ContractName = "Test";
        private const string MethodName = "TestMethod";

        private readonly SmartContractGasInjector spendGasInjector = new SmartContractGasInjector();

        private readonly ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));

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

                var persistentState = new PersistentState(this.repository, Address.Zero.ToUint160());
                var vm = new ReflectionVirtualMachine(persistentState);

                var executionContext = new SmartContractExecutionContext(new Block(0, 0, 0), new Message(Address.Zero, Address.Zero, 0, 500000), 1, new object[] { 1 });

                SmartContractExecutionResult result = vm.ExecuteMethod(injectedAssemblyBytes, ContractName, MethodName, executionContext);
                Assert.Equal(aimGasAmount, Convert.ToInt32(result.GasUsed));
            }
        }
    }
}