using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IBlockStoreQueue : IBlockStore
    {
        /// <summary>Adds a block to the batch, pending being written to disk.</summary>
        /// <param name="chainedHeaderBlock">The block and its chained header pair.</param>
        void AddToPending(ChainedHeaderBlock chainedHeaderBlock);
    }
}
