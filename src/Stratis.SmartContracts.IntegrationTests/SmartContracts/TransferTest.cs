using Stratis.SmartContracts;

[Deploy]
public class TransferTest : SmartContract
{
    public TransferTest(ISmartContractState state)
        : base(state)
    {
    }

    public void Test()
    {
        Transfer(Serializer.ToAddress("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn").Address, 100);
    }

    public void Test2()
    {
        Transfer(Serializer.ToAddress("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn").Address, 100);
        Transfer(Serializer.ToAddress("n2hyJZj9m8jorD21Nss1tbUtR1NthNHEzg").Address, 100);
    }

    public void P2KTest()
    {
        Transfer(Serializer.ToAddress("mxKorCkWmtrPoekfWiMzERJPhaT13nnkMy").Address, 100);
    }

    public bool DoNothing()
    {
        return true;
    }
}