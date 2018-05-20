namespace Stratis.Patricia
{
    public interface IPatriciaTrie : ISource<byte[],byte[]>
    {
        /// <summary>
        /// Get the 32-byte hash of the current root node.
        /// </summary>
        byte[] GetRootHash();

        /// <summary>
        /// Set the 32-byte hash of the current root node.
        /// </summary>
        /// <param name="root"></param>
        void SetRootHash(byte[] root);
    }
}
