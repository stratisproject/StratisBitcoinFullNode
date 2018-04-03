using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Stratis.Bitcoin.Features.SmartContracts.Tests.SmartContracts;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class FormatValidationTests
    {
        private static readonly SingleConstructorValidator SingleConstructorValidator = new SingleConstructorValidator();

        private static readonly ConstructorParamValidator ConstructorParamValidator = new ConstructorParamValidator();

        private static readonly SmartContractDecompiler Decompiler = new SmartContractDecompiler();

        private static readonly byte[] SingleConstructorCompilation = 
            SmartContractCompiler.CompileFile("SmartContracts/SingleConstructor.cs").Compilation;

        private static readonly SmartContractDecompilation SingleConstructorDecompilation = Decompiler.GetModuleDefinition(SingleConstructorCompilation);

        private static readonly byte[] MultipleConstructorCompilation =
            SmartContractCompiler.CompileFile("SmartContracts/MultipleConstructor.cs").Compilation;

        private static readonly SmartContractDecompilation MultipleConstructorDecompilation = Decompiler.GetModuleDefinition(MultipleConstructorCompilation);

        private static readonly byte[] InvalidParamCompilation =
            SmartContractCompiler.CompileFile("SmartContracts/InvalidParam.cs").Compilation;

        private static readonly SmartContractDecompilation InvalidParamDecompilation = Decompiler.GetModuleDefinition(InvalidParamCompilation);

        [Fact]
        public void SmartContract_ValidateFormat_HasSingleConstructorSuccess()
        {            
            IEnumerable<SmartContractValidationError> validationResult = SingleConstructorValidator.Validate(SingleConstructorDecompilation.ContractType);
            
            Assert.Empty(validationResult);
        }

        [Fact]
        public void SmartContract_ValidateFormat_HasMultipleConstructorsFails()
        {
            IEnumerable<SmartContractValidationError> validationResult = SingleConstructorValidator.Validate(MultipleConstructorDecompilation.ContractType);

            Assert.Single(validationResult);
            Assert.Equal(SingleConstructorValidator.SingleConstructorError, validationResult.Single().Message);
        }

        [Fact]
        public void SmartContract_ValidateFormat_HasInvalidFirstParamFails()
        {
            IEnumerable<SmartContractValidationError> validationResult = ConstructorParamValidator.Validate(InvalidParamDecompilation.ContractType);
            
            Assert.Single(validationResult);
            Assert.Equal(ConstructorParamValidator.InvalidParamError, validationResult.Single().Message);
        }

        [Fact]
        public void SmartContract_ValidateFormat_FormatValidatorChecksConstructor()
        {
            var validator = new SmartContractFormatValidator();
            var validationResult = validator.Validate(MultipleConstructorDecompilation);

            Assert.Single(validationResult.Errors);
            Assert.False(validationResult.IsValid);
        }
    }
}
