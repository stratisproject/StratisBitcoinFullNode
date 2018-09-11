
using Stratis.SmartContracts;

public class MemoryLimit : SmartContract
{
    public MemoryLimit(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void AllowedArray()
    {
        var arr = new int[100];
    }

    public void NotAllowedArray()
    {
        var arr = new int[10_000];
    }

    public void AllowedMultiArray()
    {
        var arr = new int[4, 4];
    }

    public void NotAllowedMultiArray()
    {
        var arr = new int[100, 100];
    }
}
