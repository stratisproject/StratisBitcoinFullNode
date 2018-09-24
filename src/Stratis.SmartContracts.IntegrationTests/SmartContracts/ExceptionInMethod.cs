
using Stratis.SmartContracts;

public class ExceptionInMethod : SmartContract
{
    public ExceptionInMethod(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void Method()
    {
        // Throws OutOfIndexException
        var array = new int[25];
        int value = array[26];
    }
}

