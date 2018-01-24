using System;
using Stratis.SmartContracts;
using System.Linq;

public class TransferTest : CompiledSmartContract
{
    public void Test()
    {
        Transfer(new Address(123), 100);
    }
}