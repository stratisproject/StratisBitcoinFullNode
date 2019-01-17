using System.IO;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Loader;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests.Loader
{
    public class ContractAssemblyTests
    {
        private readonly ContractCompilationResult compilation;
        private readonly ContractAssemblyLoader loader;

        public string Contract = Path.Combine("Loader", "Test.cs");

        public ContractAssemblyTests()
        {
            this.compilation = ContractCompiler.CompileFile(this.Contract);
            this.loader = new ContractAssemblyLoader();
        }

        [Fact]
        public void GetType_Returns_Correct_Type()
        {
            var assemblyLoadResult = this.loader.Load((ContractByteCode) this.compilation.Compilation);

            var contractAssembly = assemblyLoadResult.Value;

            var type = contractAssembly.GetType("Test");

            Assert.NotNull(type);
            Assert.Equal("Test", type.Name);
        }
    }
}
