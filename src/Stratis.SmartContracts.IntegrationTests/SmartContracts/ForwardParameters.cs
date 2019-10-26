using Stratis.SmartContracts;

public class ForwardParameters : SmartContract
{
    public ForwardParameters(ISmartContractState state,
        char aChar,
        Address anAddress,
        bool aBool,
        int anInt,
        long aLong,
        uint aUint,
        ulong aUlong,
        string aString,
        Address sendTo) : base(state)
    {
        ITransferResult result = this.Call(sendTo, this.Balance, "Call", new object[]
        {
            aChar,
            anAddress,
            aBool,
            anInt,
            aLong,
            aUint,
            aUlong,
            aString
        });

        Assert(result.Success);
    }
}

