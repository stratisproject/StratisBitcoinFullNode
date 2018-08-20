using Stratis.SmartContracts;

[Deploy]
public class TransferTestPos : SmartContract
{
    public TransferTestPos(ISmartContractState state)
        : base(state)
    {
    }

    public void Test()
    {
        Transfer(new Address("SZUEJ7EkPWGC2W1jyMZXVNnYx76vWdyB2a"), 100);
    }

    public void Test2()
    {
        Transfer(new Address("SZUEJ7EkPWGC2W1jyMZXVNnYx76vWdyB2a"), 100);
        Transfer(new Address("n2hyJZj9m8jorD21Nss1tbUtR1NthNHEzg"), 100);
    }

    public void P2KTest()
    {
        Transfer(new Address("mxKorCkWmtrPoekfWiMzERJPhaT13nnkMy"), 100);
    }

    public bool DoNothing()
    {
        return true;
    }
}