using Stratis.SmartContracts;

public class RecursiveLoopCreate : SmartContract
{
    public RecursiveLoopCreate(ISmartContractState state) : base(state)
    {
        Assert(Create<RecursiveLoopCreate>().Success);
    }
}
