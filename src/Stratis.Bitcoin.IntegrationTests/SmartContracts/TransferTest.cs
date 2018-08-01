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
        TransferFunds(new Address("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn"), 100);
    }

    public void Test2()
    {
        TransferFunds(new Address("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn"), 100);
        TransferFunds(new Address("n2hyJZj9m8jorD21Nss1tbUtR1NthNHEzg"), 100);
    }

    public void P2KTest()
    {
        TransferFunds(new Address("mxKorCkWmtrPoekfWiMzERJPhaT13nnkMy"), 100);
    }

    public bool DoNothing()
    {
        return true;
    }
}