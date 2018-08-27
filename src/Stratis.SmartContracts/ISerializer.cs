namespace Stratis.SmartContracts
{
    public interface ISerializer
    {
        byte[] Serialize(Address address);

        byte[] Serialize(bool b);

        byte[] Serialize(int i);

        byte[] Serialize(long l);

        byte[] Serialize(uint u);

        byte[] Serialize(ulong ul);

        byte[] Serialize(string s);

        bool ToBool(byte[] val);

        Address ToAddress(byte[] val);

        int ToInt32(byte[] val);

        uint ToUInt32(byte[] val);

        long ToInt64(byte[] val);

        ulong ToUInt64(byte[] val);

        string ToString(byte[] val);
    }
}
