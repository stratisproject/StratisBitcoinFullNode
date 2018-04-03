namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Leaves keys untouched.
    /// </summary>
    public class BasicKeyEncodingStrategy : IKeyEncodingStrategy
    {
        public static readonly BasicKeyEncodingStrategy Default = new BasicKeyEncodingStrategy();

        public byte[] GetBytes(byte[] key)
        {
            return key;
        }
    }
}
