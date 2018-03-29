namespace Stratis.SmartContracts.Core
{
    public interface IKeyEncodingStrategy
    {
        byte[] GetBytes(byte[] key);
    }
}
