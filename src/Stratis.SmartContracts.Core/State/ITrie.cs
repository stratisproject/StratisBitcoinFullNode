namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// </summary>
    /// <typeparam name="V"></typeparam>
    public interface ITrie<V> : ISource<byte[], V>
    {
        byte[] GetRootHash();
        void SetRoot(byte[] root);
        void Clear();
    }
}
