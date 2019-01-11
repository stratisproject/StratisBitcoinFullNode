using Stratis.SmartContracts.CLR.Compilation;
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
    }
}
