
using Stratis.SmartContracts;

public class ReturnStruct : SmartContract
{
    public ReturnStruct(ISmartContractState state) : base(state)
    {
    }

    public TheStruct CallMe()
    {
        return new TheStruct
        {
            TestBool = true,
            TestString = "My String"
        };
    }

    public struct TheStruct
    {
        public string TestString;
        public bool TestBool;
    }
}
