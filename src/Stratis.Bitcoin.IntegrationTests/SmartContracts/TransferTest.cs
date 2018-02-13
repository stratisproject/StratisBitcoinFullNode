using System;
using Stratis.SmartContracts;
using System.Linq;

public class TransferTest : SmartContract
{
    public void Test()
    {
        Transfer(new Address(123), 100);
    }

    public void Test2()
    {
        Transfer(new Address(123), 100);
        Transfer(new Address(124), 100);
    }

    public bool DoNothing()
    {
        return true;
    }
}