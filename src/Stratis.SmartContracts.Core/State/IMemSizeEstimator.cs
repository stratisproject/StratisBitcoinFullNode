namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// </summary>
    /// <typeparam name="E"></typeparam>
    public interface IMemSizeEstimator<E>
    {
        long EstimateSize(E e);
    }

    public class ByteArrayEstimator : IMemSizeEstimator<byte[]>
    {
        public long EstimateSize(byte[] bytes)
        {
            return bytes == null ? 0 : bytes.Length + 4;
        }
    }
}

