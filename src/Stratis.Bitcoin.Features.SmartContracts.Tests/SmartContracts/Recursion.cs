using System;
using Stratis.SmartContracts;

[Deploy]
public class Recursion : SmartContract
{
    public Recursion(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public bool DoRecursion()
    {
        if (DateTime.Now.Ticks % 7 == 0)
        {
            return true;
        }

        return DoRecursion();
    }
}
