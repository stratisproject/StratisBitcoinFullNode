using Stratis.SmartContracts;

public class CreateWithAllParameters : SmartContract
{
    public CreateWithAllParameters(ISmartContractState state,
        char aChar,
        Address anAddress,
        bool aBool,
        int anInt,
        long aLong,
        uint aUint,
        ulong aUlong,
        string aString,
        byte[] bytes) : base(state)
    {
        PersistentState.SetChar("char", aChar);
        PersistentState.SetAddress("Address", anAddress);
        PersistentState.SetBool("bool", aBool);
        PersistentState.SetInt32("int", anInt);
        PersistentState.SetInt64("long", aLong);
        PersistentState.SetUInt32("uint", aUint);
        PersistentState.SetUInt64("ulong",aUlong);
        PersistentState.SetString("string", aString);
        PersistentState.SetBytes("bytes", bytes);
        this.Log(new Log
        {
            Char = aChar,
            Address = anAddress,
            Bool = aBool,
            Int = anInt,
            Long = aLong,
            Uint = aUint,
            Ulong = aUlong,
            String = aString,
            Bytes = bytes
        });
        Assert(PersistentState.GetChar("char") == aChar);
        Assert(PersistentState.GetAddress("Address") == anAddress);
        Assert(PersistentState.GetBool("bool") == aBool);
        Assert(PersistentState.GetInt32("int") == anInt);
        Assert(PersistentState.GetInt64("long") == aLong);
        Assert(PersistentState.GetString("string") == aString);
        byte[] bytesStored = PersistentState.GetBytes("bytes");
        Assert(bytesStored.Length == bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
        {
            Assert(bytesStored[i] == bytes[i]);
        }
    }

    public struct Log
    {
        [Index]
        public char Char;

        [Index]
        public Address Address;

        [Index]
        public bool Bool;

        [Index]
        public int Int;

        [Index]
        public long Long;

        [Index]
        public uint Uint;

        [Index]
        public ulong Ulong;

        [Index]
        public string String;

        [Index]
        public byte[] Bytes;
    }
}