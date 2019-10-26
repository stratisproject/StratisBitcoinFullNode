using Stratis.SmartContracts;

[Deploy]
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