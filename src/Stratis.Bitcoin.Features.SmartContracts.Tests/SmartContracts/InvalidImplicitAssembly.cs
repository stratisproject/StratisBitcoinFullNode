using System.Linq;
using Stratis.SmartContracts;

public class InvalidImplicitAssembly
{
    public InvalidImplicitAssembly(ISmartContractState state)
    {
    }

    public void Test()
    {
        new string[] { }.ToList().Sort();
    }
}