using Stratis.SmartContracts;

public class TransferTest : SmartContract
{
    public TransferTest(ISmartContractState state)
        : base(state)
    {
    }

    public void Test()
    {
        TransferFunds(new Address("0x0000000000000000000000000000000000000123"), 100);
    }

    public void Test2()
    {
        TransferFunds(new Address("0x0000000000000000000000000000000000000123"), 100);
        TransferFunds(new Address("0x0000000000000000000000000000000000000124"), 100);
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