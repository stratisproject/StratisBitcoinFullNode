using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.Util;
using Xunit;

public class DemoHelperTests
{
    [Fact]
    public void GetHexStringForDemo()
    {
        var compilationResult = SmartContractCompiler.CompileFile("SmartContracts/SimpleAuction.cs");
        string example = compilationResult.Compilation.ToHexString();
    }
}