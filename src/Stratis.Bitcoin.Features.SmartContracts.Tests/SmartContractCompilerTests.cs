using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SmartContractCompilerTests
    {
        [Fact]
        public void SmartContract_Compiler_ReturnsFalse()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile("Uncompilable");

            Assert.False(compilationResult.Success);
            Assert.NotEmpty(compilationResult.Diagnostics);
            Assert.Null(compilationResult.Compilation);
        }

        [Fact]
        public void SmartContract_Compiler_ReturnsTrue()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile("class C{static void M(){}}");

            Assert.True(compilationResult.Success);
            Assert.Empty(compilationResult.Diagnostics);
            Assert.NotNull(compilationResult.Compilation);
        }

        [Fact]
        public void SmartContract_ReferenceResolver_HasCorrectAssemblies()
        {
            List<Assembly> allowedAssemblies = ReferencedAssemblyResolver.AllowedAssemblies.ToList();

            Assert.Equal(4, allowedAssemblies.Count);
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "System.Runtime");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "System.Private.CoreLib");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "Stratis.SmartContracts");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "System.Linq");
        }

        [Fact]
        public void SmartContract_Compiler_FailsOnImplicitInvalidAssemblyReference()
        {
            SmartContractCompilationResult result = SmartContractCompiler.CompileFile("SmartContracts/InvalidImplicitAssembly.cs");
            Assert.False(result.Success);
        }

        [Fact]
        public void SmartContract_Compiler_CanCompileMultipleFiles()
        {
            SmartContractCompilationResult result = SmartContractCompiler.CompileDirectory("SmartContracts", "MultipleFiles");
            Assert.True(result.Success);
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(result.Compilation);
            Assert.Contains(decomp.ModuleDefinition.Types, x => x.Name == "MultipleFiles1");
            Assert.Contains(decomp.ModuleDefinition.Types, x => x.Name == "MultipleFiles2");
        }
    }
}
