using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;
using Stratis.ModuleValidation.Net.Format;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class FormatValidationTests
    {
        private static readonly SingleConstructorValidator SingleConstructorValidator = new SingleConstructorValidator();

        private static readonly ConstructorParamValidator ConstructorParamValidator = new ConstructorParamValidator();

        private static readonly byte[] SingleConstructorCompilation = 
            SmartContractCompiler.CompileFile("SmartContracts/SingleConstructor.cs").Compilation;

        private static readonly SmartContractDecompilation SingleConstructorDecompilation = SmartContractDecompiler.GetModuleDefinition(SingleConstructorCompilation);

        private static readonly byte[] MultipleConstructorCompilation =
            SmartContractCompiler.CompileFile("SmartContracts/MultipleConstructor.cs").Compilation;

        private static readonly SmartContractDecompilation MultipleConstructorDecompilation = SmartContractDecompiler.GetModuleDefinition(MultipleConstructorCompilation);

        private static readonly byte[] AsyncVoidCompilation =
            SmartContractCompiler.CompileFile("SmartContracts/AsyncVoid.cs").Compilation;

        private static readonly SmartContractDecompilation AsyncVoidDecompilation = SmartContractDecompiler.GetModuleDefinition(AsyncVoidCompilation);
        
        private static readonly byte[] AsyncTaskCompilation =
            SmartContractCompiler.CompileFile("SmartContracts/AsyncTask.cs").Compilation;

        private static readonly SmartContractDecompilation AsyncTaskDecompilation = SmartContractDecompiler.GetModuleDefinition(AsyncTaskCompilation);

        private static readonly byte[] AsyncGenericTaskCompilation =
            SmartContractCompiler.CompileFile("SmartContracts/AsyncGenericTask.cs").Compilation;

        private static readonly SmartContractDecompilation AsyncGenericTaskDecompilation = SmartContractDecompiler.GetModuleDefinition(AsyncGenericTaskCompilation);

        private static readonly byte[] InvalidParamCompilation =
            SmartContractCompiler.CompileFile("SmartContracts/InvalidParam.cs").Compilation;

        private static readonly SmartContractDecompilation InvalidParamDecompilation = SmartContractDecompiler.GetModuleDefinition(InvalidParamCompilation);

        public static readonly byte[] ArrayInitializationCompilation = SmartContractCompiler.CompileFile("SmartContracts/ArrayInitialization.cs").Compilation;

        public static readonly SmartContractDecompilation ArrayInitializationDecompilation = SmartContractDecompiler.GetModuleDefinition(ArrayInitializationCompilation);

        [Fact]
        public void SmartContract_ValidateFormat_HasSingleConstructorSuccess()
        {            
            IEnumerable<ValidationResult> validationResult = SingleConstructorValidator.Validate(SingleConstructorDecompilation.ContractType);
            
            Assert.Empty(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_HasMultipleConstructorsFails()
        {
            IEnumerable<ValidationResult> validationResult = SingleConstructorValidator.Validate(MultipleConstructorDecompilation.ContractType);

            Assert.Single(validationResult);
            Assert.Equal(SingleConstructorValidator.SingleConstructorError, validationResult.Single().Message);
        }

        [Fact]
        public void SmartContract_ValidateFormat_HasInvalidFirstParamFails()
        {
            IEnumerable<ValidationResult> validationResult = ConstructorParamValidator.Validate(InvalidParamDecompilation.ContractType);
            
            Assert.Single(validationResult);
            Assert.Equal(ConstructorParamValidator.InvalidParamError, validationResult.Single().Message);
        }

        [Fact]
        public void SmartContract_ValidateFormat_FormatValidatorChecksConstructor()
        {
            var validator = new SmartContractFormatValidator(ReferencedAssemblyResolver.AllowedAssemblies);
            var validationResult = validator.Validate(MultipleConstructorDecompilation);

            Assert.Single(validationResult.Errors);
            Assert.False(validationResult.IsValid);
        }

        [Fact]
        public void SmartContract_ValidateFormat_AsyncVoid()
        {
            var validator = new AsyncValidator();
            TypeDefinition type = AsyncVoidDecompilation.ContractType;

            IEnumerable<ValidationResult> validationResult = validator.Validate(type);

            Assert.Single(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_AsyncTask()
        {
            var validator = new AsyncValidator();
            TypeDefinition type = AsyncTaskDecompilation.ContractType;

            IEnumerable<ValidationResult> validationResult = validator.Validate(type);

            Assert.Single(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_AsyncGenericTask()
        {
            var validator = new AsyncValidator();
            TypeDefinition type = AsyncGenericTaskDecompilation.ContractType;

            IEnumerable<ValidationResult> validationResult = validator.Validate(type);

            Assert.Single(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_ArrayInitialization()
        {
            var validator = new AsyncValidator();
            TypeDefinition type = ArrayInitializationDecompilation.ContractType;

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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new NestedTypeValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new NestedTypeValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new FieldDefinitionValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);

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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new FieldDefinitionValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);

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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            var validator = new FieldDefinitionValidator();

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);

            IEnumerable<ValidationResult> result = validator.Validate(decomp.ContractType);

            Assert.NotEmpty(result);
        }
    }
}
