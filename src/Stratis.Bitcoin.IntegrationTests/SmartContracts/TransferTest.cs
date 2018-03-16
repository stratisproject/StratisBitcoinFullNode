using Stratis.SmartContracts;

public class TransferTest : SmartContract
{
    public TransferTest(ISmartContractState state) 
        : base(state)
    {
    }

    public void Test()
    {
        Transfer(new Address("0x0000000000000000000000000000000000000123"), 100);
    }

    public void Test2()
    {
        Transfer(new Address("0x0000000000000000000000000000000000000123"), 100);
        Transfer(new Address("0x0000000000000000000000000000000000000124"), 100);
    }

    public bool DoNothing()
    {
        return true;
    }
}