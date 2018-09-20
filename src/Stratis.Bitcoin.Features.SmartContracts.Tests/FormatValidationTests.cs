﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;
using Stratis.ModuleValidation.Net.Format;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Core.Validation.Validators.Type;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class FormatValidationTests
    {
        private static readonly SingleConstructorValidator SingleConstructorValidator = new SingleConstructorValidator();

        private static readonly ConstructorParamValidator ConstructorParamValidator = new ConstructorParamValidator();

        private static readonly byte[] SingleConstructorCompilation = 
            ContractCompiler.CompileFile("SmartContracts/SingleConstructor.cs").Compilation;

        private static readonly IContractModuleDefinition SingleConstructorModuleDefinition = ContractDecompiler.GetModuleDefinition(SingleConstructorCompilation);

        private static readonly byte[] MultipleConstructorCompilation =
            ContractCompiler.CompileFile("SmartContracts/MultipleConstructor.cs").Compilation;

        private static readonly IContractModuleDefinition MultipleConstructorModuleDefinition = ContractDecompiler.GetModuleDefinition(MultipleConstructorCompilation);

        private static readonly byte[] AsyncVoidCompilation =
            ContractCompiler.CompileFile("SmartContracts/AsyncVoid.cs").Compilation;

        private static readonly IContractModuleDefinition AsyncVoidModuleDefinition = ContractDecompiler.GetModuleDefinition(AsyncVoidCompilation);
        
        private static readonly byte[] AsyncTaskCompilation =
            ContractCompiler.CompileFile("SmartContracts/AsyncTask.cs").Compilation;

        private static readonly IContractModuleDefinition AsyncTaskModuleDefinition = ContractDecompiler.GetModuleDefinition(AsyncTaskCompilation);

        private static readonly byte[] AsyncGenericTaskCompilation =
            ContractCompiler.CompileFile("SmartContracts/AsyncGenericTask.cs").Compilation;

        private static readonly IContractModuleDefinition AsyncGenericTaskModuleDefinition = ContractDecompiler.GetModuleDefinition(AsyncGenericTaskCompilation);

        private static readonly byte[] InvalidParamCompilation =
            ContractCompiler.CompileFile("SmartContracts/InvalidParam.cs").Compilation;

        private static readonly IContractModuleDefinition InvalidParamModuleDefinition = ContractDecompiler.GetModuleDefinition(InvalidParamCompilation);

        public static readonly byte[] ArrayInitializationCompilation = ContractCompiler.CompileFile("SmartContracts/ArrayInitialization.cs").Compilation;

        public static readonly IContractModuleDefinition ArrayInitializationModuleDefinition = ContractDecompiler.GetModuleDefinition(ArrayInitializationCompilation);

        [Fact]
        public void SmartContract_ValidateFormat_HasSingleConstructorSuccess()
        {            
            IEnumerable<ValidationResult> validationResult = SingleConstructorValidator.Validate(SingleConstructorModuleDefinition.ContractType);
            
            Assert.Empty(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_HasMultipleConstructorsFails()
        {
            IEnumerable<ValidationResult> validationResult = SingleConstructorValidator.Validate(MultipleConstructorModuleDefinition.ContractType);

            Assert.Single(validationResult);
            Assert.Equal(SingleConstructorValidator.SingleConstructorError, validationResult.Single().Message);
        }

        [Fact]
        public void SmartContract_ValidateFormat_HasInvalidFirstParamFails()
        {
            bool validationResult = ConstructorParamValidator.Validate(InvalidParamModuleDefinition.ContractType);
            
            Assert.True(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_FormatValidatorChecksConstructor()
        {
            var validator = new SmartContractFormatValidator();
            var validationResult = validator.Validate(MultipleConstructorModuleDefinition.ModuleDefinition);

            Assert.Single(validationResult.Errors);
            Assert.False(validationResult.IsValid);
        }

        [Fact]
        public void SmartContract_ValidateFormat_AsyncVoid()
        {
            var validator = new AsyncValidator();
            TypeDefinition type = AsyncVoidModuleDefinition.ContractType;

            IEnumerable<ValidationResult> validationResult = validator.Validate(type);

            Assert.Single(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_AsyncTask()
        {
            var validator = new AsyncValidator();
            TypeDefinition type = AsyncTaskModuleDefinition.ContractType;

            IEnumerable<ValidationResult> validationResult = validator.Validate(type);

            Assert.Single(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_AsyncGenericTask()
        {
            var validator = new AsyncValidator();
            TypeDefinition type = AsyncGenericTaskModuleDefinition.ContractType;

            IEnumerable<ValidationResult> validationResult = validator.Validate(type);

            Assert.Single(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_ArrayInitialization()
        {
            var validator = new AsyncValidator();
            TypeDefinition type = ArrayInitializationModuleDefinition.ContractType;

            IEnumerable<ValidationResult> validationResult = validator.Validate(type);

            Assert.Empty(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_One_CustomStruct()
        {
            var adjustedSource = @"
                using System;
                using Stratis.SmartContracts;

                public class StructTest : SmartContract
                {
                    public struct Item
                    {
                        public int Number;
                        public string Name;
                    }

                    public StructTest(ISmartContractState state) : base(state)
                    {
                    }
                }
            ";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new NestedTypesAreValueTypesValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes);
            IEnumerable<ValidationResult> result = validator.Validate(decomp.ContractType);

            Assert.Empty(result);
        }

        [Fact]
        public void SmartContract_ValidateFormat_Two_CustomStructs()
        {
            var adjustedSource = @"
                using System;
                using Stratis.SmartContracts;

                public class StructTest : SmartContract
                {
                    public struct Item
                    {
                        public int Number;
                        public string Name;
                    }

                    public struct Nested
                    {
                        public Item AnItem;
                        public int Id;
                    }

                    public StructTest(ISmartContractState state) : base(state)
                    {
                    }
                }
            ";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new NestedTypesAreValueTypesValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes);
            IEnumerable<ValidationResult> result = validator.Validate(decomp.ContractType);

            Assert.Empty(result);
        }

        [Fact]
        public void SmartContract_ValidateFormat_ConstantField_Success()
        {
            var adjustedSource = @"
                using System;
                using Stratis.SmartContracts;

                public class FieldTest : SmartContract
                {
                    private const int field1 = 12345;

                    public FieldTest(ISmartContractState state) : base(state)
                    {
                    }
                }
            ";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new FieldDefinitionValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes);

            IEnumerable<ValidationResult> result = validator.Validate(decomp.ContractType);

            Assert.Empty(result);
        }

        [Fact]
        public void SmartContract_ValidateFormat_UnitializedReadonlyField_Fails()
        {
            var adjustedSource = @"
                using System;
                using Stratis.SmartContracts;

                public class FieldTest : SmartContract
                {
                    private readonly int field1;

                    public FieldTest(ISmartContractState state) : base(state)
                    {
                    }
                }
            ";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new FieldDefinitionValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes);

            IEnumerable<ValidationResult> result = validator.Validate(decomp.ContractType);

            Assert.NotEmpty(result);
        }

        [Fact]
        public void SmartContract_ValidateFormat_InitializedReadonlyField_Fails()
        {
            var adjustedSource = @"
                using System;
                using Stratis.SmartContracts;

                public class FieldTest : SmartContract
                {
                    private readonly int field1 = 1234567;

                    public FieldTest(ISmartContractState state) : base(state)
                    {
                    }
                }
            ";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new FieldDefinitionValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes);

            IEnumerable<ValidationResult> result = validator.Validate(decomp.ContractType);

            Assert.NotEmpty(result);
        }

        [Fact]
        public void SmartContract_ValidateFormat_AssemblyReferences()
        {
            var adjustedSource = @"
using System;
using System.Linq;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state) : base(state)
    {
        IQueryable q = null;    
    }
}
";
            ContractCompilationResult compilationResult = Compile(adjustedSource, new [] { typeof(IQueryable).Assembly.Location });
            Assert.True(compilationResult.Success);

            var validator = new SmartContractFormatValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes);

            var result = validator.Validate(decomp.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
        }

        [Fact]
        public void SmartContract_ValidateFormat_TwoTypes()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state) : base(state) {}
}


public class Test2 {
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new SmartContractFormatValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes);

            var result = validator.Validate(decomp.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Equal(2, result.Errors.Count());
        }

        /// <summary>
        /// Get the compiled bytecode for the specified C# source code.
        /// </summary>
        /// <param name="source"></param>
        public static ContractCompilationResult Compile(string source, IEnumerable<string> additionalReferencePaths = null)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            var references = GetReferences().ToList();
            
            if (additionalReferencePaths != null)
                references.AddRange(additionalReferencePaths.Select(path => MetadataReference.CreateFromFile(path)));

            CSharpCompilation compilation = CSharpCompilation.Create(
                "smartContract",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    checkOverflow: true));


            using (var dllStream = new MemoryStream())
            {
                EmitResult emitResult = compilation.Emit(dllStream);
                if (!emitResult.Success)
                    return ContractCompilationResult.Failed(emitResult.Diagnostics);

                return ContractCompilationResult.Succeeded(dllStream.ToArray());
            }
        }

        private static IEnumerable<MetadataReference> GetReferences()
        {
            return ReferencedAssemblyResolver.AllowedAssemblies.Select(a => MetadataReference.CreateFromFile(a.Location));
        }
    }
}
