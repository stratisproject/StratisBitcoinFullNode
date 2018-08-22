using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;

// Note this contract is non-deterministic and will fail to be deployed.
// For internal testing purposes only.

[Deploy]
public class Recursion : SmartContract
{
    public Recursion(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public bool DoRecursion()
    {
        if (DateTimeProvider.Default.GetUtcNow().Ticks % 7 == 0)
        {
            return true;
        }

        return DoRecursion();
    }
}
