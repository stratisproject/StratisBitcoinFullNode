using Stratis.SmartContracts;

public class CallWithAllParameters : SmartContract
{
    public CallWithAllParameters(ISmartContractState state) : base(state)
    {
    }

    public void Call(
        char aChar,
        Address anAddress,
        bool aBool,
        int anInt,
        long aLong,
        uint aUint,
        ulong aUlong,
        string aString)
    {
        byte[] charBytes = this.Serializer.Serialize(aChar);
        char charResult = this.Serializer.ToChar(charBytes);
        Assert(aChar == charResult , "Char failed.");

        byte[] addressBytes = this.Serializer.Serialize(anAddress);
        var result = this.Serializer.ToAddress(addressBytes);
        Assert(result == anAddress, "Address failed.");

        byte[] boolBytes = this.Serializer.Serialize(aBool);
        bool boolResult = this.Serializer.ToBool(boolBytes);
        Assert(boolResult == aBool, "Bool failed.");

        byte[] intBytes = this.Serializer.Serialize(anInt);
        int intResult = this.Serializer.ToInt32(intBytes);
        Assert(intResult == anInt, "Int failed.");

        byte[] longBytes = this.Serializer.Serialize(aLong);
        long longResult = this.Serializer.ToInt64(longBytes);
        Assert(longResult == aLong, "Long failed.");

        byte[] uintBytes = this.Serializer.Serialize(aUint);
        uint uintResult = this.Serializer.ToUInt32(uintBytes);
        Assert(uintResult == aUint, "Uint failed.");

        byte[] ulongBytes = this.Serializer.Serialize(aUlong);
        ulong ulongResult = this.Serializer.ToUInt64(ulongBytes);
        Assert(ulongResult == aUlong, "Ulong failed.");

        byte[] stringBytes = this.Serializer.Serialize(aString);
        string stringResult = this.Serializer.ToString(stringBytes);
        Assert(stringResult == aString, "String failed.");
    }
}

