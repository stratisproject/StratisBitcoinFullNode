using System;
using Stratis.SmartContracts;

public class InterContract1 : SmartContract
{
    public InterContract1(SmartContractState state) : base(state) {}

    public int ReturnInt()
    {
        PersistentState.SetObject(0, "testString");
        return Convert.ToInt32(Balance);
    }
}
