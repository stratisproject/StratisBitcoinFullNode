using System.IO;
using System.Runtime.Loader;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Loader;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests.Loader
{
    public class ContractAssemblyLoaderTests
    {
        private readonly ContractCompilationResult compilation;
        private readonly ContractAssemblyLoader loader;

        public string Contract = Path.Combine("Loader", "Test.cs");

        public ContractAssemblyLoaderTests()
        {
            this.compilation = ContractCompiler.CompileFile(this.Contract);
            this.loader = new ContractAssemblyLoader();
        }

        [Fact]
        public void Does_Not_Load_Assembly_Into_Default_AssemblyLoadContext()
        {
            var assemblyLoadResult = this.loader.Load((ContractByteCode) this.compilation.Compilation);

            Assert.True(assemblyLoadResult.IsSuccess);

            var loadContext = AssemblyLoadContext.GetLoadContext(assemblyLoadResult.Value.Assembly);

            Assert.NotEqual(AssemblyLoadContext.Default, loadContext);
        }
    }
}
