using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Moq;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.CLR.Validation;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ContractModuleDefinitionTests
    {
        private byte[] contractCode;

        public ContractModuleDefinitionTests()
        {
            this.contractCode = ContractCompiler.Compile(@"
using Stratis.SmartContracts;

public class ModuleDefinitionTest : SmartContract
{
    public Address Owner { get; set; }

    public ModuleDefinitionTest(ISmartContractState smartContractState): base(smartContractState)
    {
    }
}
").Compilation;           
        }

        [Fact]
        public void Resolve_Property_Name_Success()
        {
            var readResult = ContractDecompiler.GetModuleDefinition(this.contractCode);
            var contractModule = readResult.Value;
            var methodName = contractModule.GetPropertyGetterMethodName("ModuleDefinitionTest", "Owner");
            Assert.Equal("get_Owner", methodName);
        }

        [Fact]
        public void Resolve_Property_On_Nonexistent_Type()
        {
            var readResult = ContractDecompiler.GetModuleDefinition(this.contractCode);
            var contractModule = readResult.Value;
            var methodName = contractModule.GetPropertyGetterMethodName("DoesntExist", "Owner");
            Assert.Null(methodName);
        }

        [Fact]
        public void Resolve_Property_Doesnt_Exist()
        {
            var readResult = ContractDecompiler.GetModuleDefinition(this.contractCode);
            var contractModule = readResult.Value;
            var methodName = contractModule.GetPropertyGetterMethodName("ModuleDefinitionTest", "DoesntExist");
            Assert.Null(methodName);
        }

        [Fact]
        public void Validate_Invalid_ModuleDefinition_Catches_Exceptions()
        {
            var contractModule = new ContractModuleDefinition(null, null);

            var validator = new Mock<ISmartContractValidator>();
            validator
                .Setup(v => v.Validate(It.IsAny<ModuleDefinition>()))
                .Throws(new Exception("Invalid operation"));

            var result = contractModule.Validate(validator.Object);

            Assert.False(result.IsValid);
        }
    }
}
