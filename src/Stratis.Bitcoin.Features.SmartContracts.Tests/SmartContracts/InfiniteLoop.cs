﻿using Stratis.SmartContracts;

public class InfiniteLoop : SmartContract
{
    public InfiniteLoop(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void Loop()
    {
        while(true) { }
    }
}
