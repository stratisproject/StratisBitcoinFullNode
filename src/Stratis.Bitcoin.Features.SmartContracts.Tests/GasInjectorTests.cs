using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

                                            public class Test : CompiledSmartContract
                                            {
                                                public void TestMethod(int number)
                                                {
                                                    int test = 11 + number;
                                                    var things = new string[]{""Something"", ""SomethingElse""};
                                                    test += things.Length;
                                                }
                                            }";

        private const string ContractName = "Test";
        private const string MethodName = "TestMethod";

        private readonly SmartContractGasInjector _spendGasInjector = new SmartContractGasInjector();

        private readonly ContractStateRepositoryRoot _repository =
            new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            
        // TODO: Right now the gas injector is only taking into account the instructions
        // in the user-defined methods. Calls to System methods aren't increasing the instructions.
        // Need to work this out somehow. Averages?

        // ALSO, write tests to check the different branches of code

        [Fact]
        public void TestGasInjector()
        {
            var originalAssemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(TestSource);

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(AppContext.BaseDirectory);
            ModuleDefinition moduleDefinition = ModuleDefinition.ReadModule(new MemoryStream(originalAssemblyBytes), new ReaderParameters { AssemblyResolver = resolver });
            var contractType = moduleDefinition.GetType(ContractName);
            var baseType = contractType.BaseType.Resolve();
            var testMethod = contractType.Methods.FirstOrDefault(x => x.Name == MethodName);
            var constructorMethod = contractType.Methods.FirstOrDefault(x => x.Name.Contains("ctor"));
            int aimGasAmount = testMethod.Body.Instructions.Count + constructorMethod.Body.Instructions.Count;

            _spendGasInjector.AddGasCalculationToContract(contractType, baseType);

            var mem = new MemoryStream();
            moduleDefinition.Write(mem);
            var injectedAssemblyBytes = mem.ToArray();

            var vm = new ReflectionVirtualMachine(_repository);
            var result = vm.ExecuteMethod(injectedAssemblyBytes, new SmartContractExecutionContext
            {
                ContractTypeName = ContractName,
                ContractMethod = MethodName,
                GasLimit = 500000,
                Parameters = new object[] { 1 }
            });

            Assert.Equal(aimGasAmount, Convert.ToInt32(result.GasUsed));
        }
    }
}
