using System;
using Stratis.SmartContracts;

public class InterContract1 : SmartContract
{
    public InterContract1(ISmartContractState state) : base(state) {}

    public int ReturnInt()
    {
        PersistentState.SetObject("test", "testString");
        return Convert.ToInt32(Balance);
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}
