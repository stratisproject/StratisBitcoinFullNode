

using Stratis.SmartContracts;

public class ArrayInitialization : SmartContract
{
    protected ArrayInitialization(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void Test()
    {
        var test = new int[] { 1, 2, 3, 4, 5 };
        var test2 = new string[] { "sample" };
        var bytes = new byte[5];
    }
}

