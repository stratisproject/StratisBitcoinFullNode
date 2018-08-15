using System.Runtime.Loader;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Loader;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Loader
{
    public class ContractAssemblyLoaderTests
    {
        private readonly SmartContractCompilationResult compilation;
        private readonly ContractAssemblyLoader loader;

        public const string Contract = @"Loader\Test.cs";

        public ContractAssemblyLoaderTests()
        {
            this.compilation = SmartContractCompiler.CompileFile(Contract);
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
