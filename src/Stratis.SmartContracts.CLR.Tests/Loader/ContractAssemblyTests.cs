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

        [Fact]
        public void GetDeployedType_Returns_Correct_Type()
        {
            var code = @"
namespace Stratis.SmartContracts.CLR.Tests.Loader
{
    [Deploy]
    public class Test : SmartContract
    {
        public Test(ISmartContractState state)
            : base(state)
        { }
    }

    public class NotDeployedType : SmartContract
    {
        public NotDeployedType(ISmartContractState state)
            :base(state)
        { }
    }
}
";
            var assemblyLoadResult = this.loader.Load((ContractByteCode) ContractCompiler.Compile(code).Compilation);

            var contractAssembly = assemblyLoadResult.Value;

            var type = contractAssembly.GetDeployedType();

            Assert.NotNull(type);
            Assert.Equal("Test", type.Name);
        }

        [Fact]
        public void GetDeployedType_NoAttribute_Returns_Correct_Type()
        {
            var code = @"
namespace Stratis.SmartContracts.CLR.Tests.Loader
{
    public class Test : SmartContract
    {
        public Test(ISmartContractState state)
            : base(state)
        { }
    }
}
";
            var assemblyLoadResult = this.loader.Load((ContractByteCode)ContractCompiler.Compile(code).Compilation);

            var contractAssembly = assemblyLoadResult.Value;

            var type = contractAssembly.GetDeployedType();

            Assert.NotNull(type);
            Assert.Equal("Test", type.Name);
        }
    }
}
