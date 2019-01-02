using Mono.Cecil;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ContractModuleDefinitionTests
    {
        private byte[] contractCode;

        public ContractModuleDefinitionTests()
        {
            this.contractCode = ContractCompiler.CompileFile("SmartContracts/Auction.cs").Compilation;           
        }

        [Fact]
        public void Resolve_Property_Name_Success()
        {
            var readResult = ContractDecompiler.GetModuleDefinition(this.contractCode);
            var contractModule = readResult.Value;
            var methodName = contractModule.GetPropertyGetterMethodName("Auction", "Owner");
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
            var methodName = contractModule.GetPropertyGetterMethodName("Auction", "DoesntExist");
            Assert.Null(methodName);
        }
    }
}
