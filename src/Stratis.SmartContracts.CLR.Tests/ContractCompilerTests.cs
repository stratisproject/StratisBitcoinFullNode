using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts.CLR.Compilation;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ContractCompilerTests
    {
        [Fact]
        public void SmartContract_Compiler_ReturnsFalse()
        {
            ContractCompilationResult compilationResult = ContractCompiler.Compile("Uncompilable");

            Assert.False(compilationResult.Success);
            Assert.NotEmpty(compilationResult.Diagnostics);
            Assert.Null(compilationResult.Compilation);
        }

        [Fact]
        public void SmartContract_Compiler_ReturnsTrue()
        {
            ContractCompilationResult compilationResult = ContractCompiler.Compile("class C{static void M(){}}");

            Assert.True(compilationResult.Success);
            Assert.Empty(compilationResult.Diagnostics);
            Assert.NotNull(compilationResult.Compilation);
        }

        [Fact]
        public void SmartContract_ReferenceResolver_HasCorrectAssemblies()
        {
            List<Assembly> allowedAssemblies = ReferencedAssemblyResolver.AllowedAssemblies.ToList();

            Assert.Equal(5, allowedAssemblies.Count);
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "System.Runtime");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "System.Private.CoreLib");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "Stratis.SmartContracts");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "System.Linq");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "Stratis.SmartContracts.Standards");
        }

        [Fact]
        public void SmartContract_Compiler_FailsOnImplicitInvalidAssemblyReference()
        {
            ContractCompilationResult result = ContractCompiler.Compile(@"
using System.Linq;
using Stratis.SmartContracts;

public class InvalidImplicitAssembly : SmartContract
{
    public InvalidImplicitAssembly(ISmartContractState state) : base(state)
    {
    }

    public void Test()
    {
        new string[] { }.ToList().Sort();
    }
}
");
            Assert.False(result.Success);
        }

        [Fact]
        public void SmartContract_Compiler_CanCompileMultipleFiles()
        {
            ContractCompilationResult result = ContractCompiler.CompileDirectory("SmartContracts", "MultipleFiles");
            Assert.True(result.Success);
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(result.Compilation).Value;
            Assert.Contains(decomp.ModuleDefinition.Types, x => x.Name == "MultipleFiles1");
            Assert.Contains(decomp.ModuleDefinition.Types, x => x.Name == "MultipleFiles2");
        }
    }
}
