using System;
using Stratis.SmartContracts;

public class TryCatch : SmartContract
{
    protected TryCatch(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public bool Test(string input)
    {
        string newString = input + "Kindness";
        try
        {
            throw new Exception();
            return true;
        }
        catch (ArgumentException e)
        {
            string lastString = e.Message + "Strategy";
            return false;
        }
    }
}