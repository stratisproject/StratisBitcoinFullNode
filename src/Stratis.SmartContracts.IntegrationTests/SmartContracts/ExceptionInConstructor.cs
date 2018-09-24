using Stratis.SmartContracts;

public class ExceptionInConstructor : SmartContract
{
    public ExceptionInConstructor(ISmartContractState smartContractState) : base(smartContractState)
    {
        // Throws OutOfIndexException
        var array = new int[25];
        int value = array[26];
    }

}