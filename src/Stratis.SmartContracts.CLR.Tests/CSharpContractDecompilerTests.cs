using CSharpFunctionalExtensions;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Decompilation;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class CSharpContractDecompilerTests
    {
        private readonly CSharpContractDecompiler decompiler;

        public CSharpContractDecompilerTests()
        {
            this.decompiler = new CSharpContractDecompiler();
        }

        [Fact]
        public void Null_Returns_Error()
        {
            Result<string> result = this.decompiler.GetSource(null);
            Assert.True(result.IsFailure);
        }

        [Fact]
        public void NotByteCode_Returns_Error()
        {
            byte[] notBytecode = new byte[]{0x01, 0x02, 0x03};
            Result<string> result = this.decompiler.GetSource(notBytecode);
            Assert.True(result.IsFailure);
        }

        [Fact]
        public void Basic_Contract_Decompiles()
        {
            byte[] contractBytes = ContractCompiler.CompileFile("SmartContracts/Auction.cs").Compilation;
            Result<string> result = this.decompiler.GetSource(contractBytes);
            Assert.True(result.IsSuccess);
            Assert.Contains("public class Auction", result.Value);
        }

        [Fact]
        public void Multiple_Classes_Decompile()
        {
            byte[] contractBytes = ContractCompiler.CompileFile("SmartContracts/ContractCreation.cs").Compilation;
            Result<string> result = this.decompiler.GetSource(contractBytes);
            Assert.True(result.IsSuccess);
            Assert.Contains("public class CatOwner : SmartContract", result.Value);
            Assert.Contains("public class Cat : SmartContract", result.Value);
        }
    }
}
