using Stratis.SmartContracts;

public class ExceptionInConstructor : SmartContract
{
    public ExceptionInConstructor(ISmartContractState smartContractState) : base(smartContractState)
    {
        // Create log that ultimately shouldn't be persisted due to failure.
        Log(new ArbitraryLog { Id = 1 });

        // Create transfer that ultimately shouldn't be persisted due to failure.
        Transfer(Serializer.ToAddress("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn"), 1);

        // Create storage that ultimately shouldn't be persisted due to failure. 
        PersistentState.SetBool("Test", true);

        // Throw (unexpected) OutOfIndexException
        var array = new int[25];
        int value = array[26];
    }

    public struct ArbitraryLog
    {
        [Index]
        public int Id;
    }
}