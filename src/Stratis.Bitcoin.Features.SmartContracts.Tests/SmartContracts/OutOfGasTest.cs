using Stratis.SmartContracts;

public class OutOfGasTest : SmartContract
{
    public OutOfGasTest(ISmartContractState state)
        : base(state)
    {
    }

    public void UseAllGas()
    {
        while (true)
        {
        }
    }
}