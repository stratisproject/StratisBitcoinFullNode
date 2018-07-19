using Stratis.SmartContracts;

[ToDeploy]
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