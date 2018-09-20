﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
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

            Assert.Equal(4, allowedAssemblies.Count);
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "System.Runtime");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "System.Private.CoreLib");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "Stratis.SmartContracts");
            Assert.Contains(allowedAssemblies, a => a.GetName().Name == "System.Linq");
        }

        [Fact]
        public void SmartContract_Compiler_FailsOnImplicitInvalidAssemblyReference()
        {
            ContractCompilationResult result = ContractCompiler.CompileFile("SmartContracts/InvalidImplicitAssembly.cs");
            Assert.False(result.Success);
        }

        [Fact]
        public void SmartContract_Compiler_CanCompileMultipleFiles()
        {
            ContractCompilationResult result = ContractCompiler.CompileDirectory("SmartContracts", "MultipleFiles");
            Assert.True(result.Success);
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(result.Compilation);
            Assert.Contains(decomp.ModuleDefinition.Types, x => x.Name == "MultipleFiles1");
            Assert.Contains(decomp.ModuleDefinition.Types, x => x.Name == "MultipleFiles2");
        }
    }
}
