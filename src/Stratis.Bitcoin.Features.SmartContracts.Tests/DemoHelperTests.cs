using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;
using Xunit;

public class DemoHelperTests
{
    [Fact]
    public void GetHexStringForDemo()
    {
        string example = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/Demo.cs").ToHexString();
    }
}