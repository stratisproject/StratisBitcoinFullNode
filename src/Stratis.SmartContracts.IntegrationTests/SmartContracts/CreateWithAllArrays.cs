using System;
using Stratis.SmartContracts;

public class CreateWithAllArrays : SmartContract
{
    public CreateWithAllArrays(ISmartContractState state,
        byte[] charBytes,
        byte[] addressBytes,
        byte[] boolBytes,
        byte[] intBytes,
        byte[] longBytes,
        byte[] uintBytes,
        byte[] ulongBytes,
        byte[] stringBytes) 
        : base(state)
    {
        char[] chars = this.Serializer.ToArray<char>(charBytes);
        Assert(chars[0] == 'a');
        Assert(chars[1] == '9');
        this.PersistentState.SetArray("chars", chars);

        Address[] addresses = this.Serializer.ToArray<Address>(addressBytes);
        Assert(addresses[0] == this.Message.Sender);
        Assert(addresses[1] == this.Serializer.ToAddress("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn"));
        this.PersistentState.SetArray("addresses", addresses);

        bool[] bools = this.Serializer.ToArray<bool>(boolBytes);
        Assert(bools[0] == false);
        Assert(bools[1] == true);
        Assert(bools[2] == false);
        this.PersistentState.SetArray("bools", bools);

        int[] ints = this.Serializer.ToArray<int>(intBytes);
        Assert(ints[0] == 1);
        Assert(ints[1] == -123);
        Assert(ints[2] == Int32.MaxValue);
        this.PersistentState.SetArray("ints", ints);

        long[] longs = this.Serializer.ToArray<long>(longBytes);
        Assert(longs[0] == 1);
        Assert(longs[1] == -123);
        Assert(longs[2] == Int64.MaxValue);
        this.PersistentState.SetArray("longs", longs);

        uint[] uints = this.Serializer.ToArray<uint>(uintBytes);
        Assert(uints[0] == 1);
        Assert(uints[1] == 123);
        Assert(uints[2] == UInt32.MaxValue);
        this.PersistentState.SetArray("uints", uints);

        ulong[] ulongs = this.Serializer.ToArray<ulong>(ulongBytes);
        Assert(ulongs[0] == 1);
        Assert(ulongs[1] == 123);
        Assert(ulongs[2] == UInt64.MaxValue);
        this.PersistentState.SetArray("ulongs", ulongs);

        string[] strings = this.Serializer.ToArray<string>(stringBytes);
        Assert(strings[0] == "Test");
        //Assert(strings[1] == ""); TODO: Uncomment this when null bug fixed
        Assert(strings[2] == "The quick brown fox jumps over the lazy dog");
        this.PersistentState.SetArray("strings", strings);
    }
}
