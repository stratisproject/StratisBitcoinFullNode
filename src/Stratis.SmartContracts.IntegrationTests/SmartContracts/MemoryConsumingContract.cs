using Stratis.SmartContracts;

public class MemoryConsumingContract : SmartContract
{
    public MemoryConsumingContract(ISmartContractState state) : base(state)
    {
    }

    public int UseTooMuchMemory(int input)
    {
        int[] test = new int[input];
        return test[0];
    }
}