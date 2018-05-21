namespace Stratis.Patricia
{
    public interface IHasher
    {
        /// <summary>
        /// Returns a hash of the given bytes.
        /// </summary>
        byte[] Hash(byte[] input);
    }
}
