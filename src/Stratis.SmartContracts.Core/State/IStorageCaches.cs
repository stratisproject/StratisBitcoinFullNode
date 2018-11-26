using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    public interface IStorageCaches
    {
        /// <summary>
        /// Get the storage source for a particular contract.
        /// </summary>
        ISource<byte[], byte[]> Get(byte[] key);

        /// <summary>
        /// Flush all of the storage sources inside.
        /// </summary>
        bool Flush();
    }
}
