﻿using System;
using Stratis.SmartContracts;

public sealed class ContractConstructorInvalid : SmartContract
{
    public ContractConstructorInvalid(ISmartContractState state)
        : base(state)
    {
        throw new Exception("test");
    }
}