using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.CLR.Validation.Validators.Type;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class FormatValidationTests
    {
        private static readonly SingleConstructorValidator SingleConstructorValidator = new SingleConstructorValidator();

        private static readonly ConstructorParamValidator ConstructorParamValidator = new ConstructorParamValidator();

        private static readonly byte[] SingleConstructorCompilation = 
            ContractCompiler.CompileFile("SmartContracts/SingleConstructor.cs").Compilation;

        private static readonly IContractModuleDefinition SingleConstructorModuleDefinition = ContractDecompiler.GetModuleDefinition(SingleConstructorCompilation).Value;

        private static readonly byte[] MultipleConstructorCompilation =
            ContractCompiler.CompileFile("SmartContracts/MultipleConstructor.cs").Compilation;

        private static readonly IContractModuleDefinition MultipleConstructorModuleDefinition = ContractDecompiler.GetModuleDefinition(MultipleConstructorCompilation).Value;

        private static readonly byte[] InvalidParamCompilation =
            ContractCompiler.CompileFile("SmartContracts/InvalidParam.cs").Compilation;

        private static readonly IContractModuleDefinition InvalidParamModuleDefinition = ContractDecompiler.GetModuleDefinition(InvalidParamCompilation).Value;

        public static readonly byte[] ArrayInitializationCompilation = ContractCompiler.CompileFile("SmartContracts/ArrayInitialization.cs").Compilation;

        public static readonly IContractModuleDefinition ArrayInitializationModuleDefinition = ContractDecompiler.GetModuleDefinition(ArrayInitializationCompilation).Value;

        [Fact]
        public void SmartContract_ValidateFormat_HasSingleConstructorSuccess()
        {
            IEnumerable<ValidationResult> validationResult = SingleConstructorValidator.Validate(SingleConstructorModuleDefinition.ModuleDefinition.GetType("SingleConstructor"));
            
            Assert.Empty(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_HasMultipleConstructorsFails()
        {
            IEnumerable<ValidationResult> validationResult = SingleConstructorValidator.Validate(MultipleConstructorModuleDefinition.ModuleDefinition.GetType("MultipleConstructor"));

            Assert.Single<ValidationResult>(validationResult);
            Assert.Equal((string) SingleConstructorValidator.SingleConstructorError, (string) validationResult.Single().Message);
        }

        [Fact]
        public void SmartContract_ValidateFormat_HasInvalidFirstParamFails()
        {
            bool validationResult = ConstructorParamValidator.Validate(InvalidParamModuleDefinition.ModuleDefinition.GetType("InvalidParam"));
            
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
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
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
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
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
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

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
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

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
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

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
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

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
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = validator.Validate(decomp.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void SmartContract_ValidateFormat_NewObj_Fails()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state) : base(state) {}

    public void CreateNewObject() {
        var obj = new object();
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new SmartContractFormatValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = validator.Validate(decomp.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsType<NewObjValidator.NewObjValidationResult>(result.Errors.First());
        }

        [Fact]
        public void SmartContract_ValidateFormat_NewStruct_Succeeds()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public struct Item
    {
        public int Number;
        public string Name;
    }

    public Test(ISmartContractState state) : base(state) {}

    public void CreateNewStruct() {
        var item = new Item();
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new SmartContractFormatValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = validator.Validate(decomp.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void SmartContract_ValidateFormat_NewArray_Succeeds()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state) : base(state) {}

    public void CreateNewStruct() {
        var item = new [] { 1, 2, 3, 4, 5 };
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new SmartContractFormatValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = validator.Validate(decomp.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void SmartContract_ValidateFormat_NewShortArray_Succeeds()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state) : base(state) {}

    public void CreateNewStruct() {
        var item = new [] { 1 };
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new SmartContractFormatValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = validator.Validate(decomp.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        /// <summary>
        /// Get the compiled bytecode for the specified C# source code.
        /// </summary>
        /// <param name="source"></param>
        public static ContractCompilationResult Compile(string source, IEnumerable<string> additionalReferencePaths = null)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            var references = Enumerable.ToList<MetadataReference>(GetReferences());
            
            if (additionalReferencePaths != null)
                references.AddRange(Enumerable.Select<string, PortableExecutableReference>(additionalReferencePaths, path => MetadataReference.CreateFromFile(path)));

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
