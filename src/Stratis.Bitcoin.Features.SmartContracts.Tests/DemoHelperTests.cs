using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.CLR.Compilation;
using Xunit;

public class DemoHelperTests
{
    [Fact]
    public void GetHexStringForDemo()
    {
        ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Auction.cs");
        string example = compilationResult.Compilation.ToHexString();
    }
}