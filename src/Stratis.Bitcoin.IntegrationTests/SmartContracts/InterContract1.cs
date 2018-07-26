using System;
using Stratis.SmartContracts;

[Deploy]
public class InterContract1 : SmartContract
{
    public InterContract1(ISmartContractState state) : base(state) { }

    public int ReturnInt()
    {
        this.PersistentState.SetString("test", "testString");
        return Convert.ToInt32(this.Balance);
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}