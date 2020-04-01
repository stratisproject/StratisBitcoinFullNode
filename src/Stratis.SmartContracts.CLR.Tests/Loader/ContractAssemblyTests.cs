using System.IO;
using System.Linq;
using System.Reflection;
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
        public void GetDeployedType_SingleType_Returns_Correct_Type()
        {
            var code = @"
using Stratis.SmartContracts;

public class SingleType : SmartContract
{
    public SingleType(ISmartContractState state) : base(state) {}
}";
            var compilation = ContractCompiler.Compile(code);

            var assemblyLoadResult = this.loader.Load((ContractByteCode)compilation.Compilation);

            var contractAssembly = assemblyLoadResult.Value;

            var type = contractAssembly.DeployedType;

            Assert.NotNull(type);
            Assert.Equal("SingleType", type.Name);
        }

        [Fact]
        public void GetDeployedType_DeployAttribute_Returns_Correct_Type()
        {
            var code = @"
using Stratis.SmartContracts;

[Deploy]
public class SingleType : SmartContract
{
    public SingleType(ISmartContractState state) : base(state) {}
}";
            var compilation = ContractCompiler.Compile(code);

            var assemblyLoadResult = this.loader.Load((ContractByteCode)compilation.Compilation);

            var contractAssembly = assemblyLoadResult.Value;

            var type = contractAssembly.DeployedType;

            Assert.NotNull(type);
            Assert.Equal("SingleType", type.Name);
        }

        [Fact]
        public void GetDeployedType_MultipleTypes_DeployAttribute_Returns_Correct_Type()
        {
            var code = @"
using Stratis.SmartContracts;

[Deploy]
public class TypeOne : SmartContract
{
    public TypeOne(ISmartContractState state) : base(state) {}
}

public class TypeTwo : SmartContract
{
    public TypeTwo(ISmartContractState state) : base(state) {}
}
";
            var compilation = ContractCompiler.Compile(code);

            var assemblyLoadResult = this.loader.Load((ContractByteCode)compilation.Compilation);

            var contractAssembly = assemblyLoadResult.Value;

            var type = contractAssembly.DeployedType;

            Assert.NotNull(type);
            Assert.Equal("TypeOne", type.Name);
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

        [Fact]
        public void GetPublicMethodsAndProperties_Returns_Correct_Properties()
        {
            var code = @"
namespace Stratis.SmartContracts.CLR.Tests.Loader
{
    public class Test : SmartContract
    {
        public Test(ISmartContractState state)
            : base(state)
        { }

        public bool TestPublicProperty {get; set;}
        public bool TestPublicPropertyPrivateSetter {get; private set;}
        private bool TestPrivateProperty {get; set;}
    }
}
";
            var assemblyLoadResult = this.loader.Load((ContractByteCode)ContractCompiler.Compile(code).Compilation);

            var contractAssembly = assemblyLoadResult.Value;

            var methodsAndProperties = contractAssembly.GetPublicGetterProperties();

            Assert.NotNull(methodsAndProperties);
            Assert.Equal(2, methodsAndProperties.Count());
        }
    }
}
