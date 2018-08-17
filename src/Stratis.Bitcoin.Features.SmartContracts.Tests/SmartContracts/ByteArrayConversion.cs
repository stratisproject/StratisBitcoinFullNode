using Stratis.SmartContracts;

public class ByteArrayConversion : SmartContract
{
    public ByteArrayConversion(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public static int BytesToInt(byte[] value)
    {
        return value[0] | (value[1] << 8) | (value[2] << 16) | (value[3] << 24);
    }

    public static uint BytesToUInt(byte[] value)
    {
        return (uint) value[0] | (uint) (value[1] << 8) | (uint) (value[2] << 16) | (uint) (value[3] << 24);
    }

    public static byte[] IntToBytes(int value)
    {
        unchecked
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            bytes[2] = (byte)(value >> 16);
            bytes[3] = (byte)(value >> 24);
            return bytes;
        }
    }

    public static byte[] UIntToBytes(uint value)
    {
        unchecked
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            bytes[2] = (byte)(value >> 16);
            bytes[3] = (byte)(value >> 24);
            return bytes;
        }
    }
}
