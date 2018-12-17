using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// Prunes the block store database by deleting blocks lower than a certain height.
    /// </summary>
    public interface IPrunedBlockRepository
    {
        /// <summary>
        /// INitializes the pruned block repository.
        /// </summary>
        /// <returns>The awaited task.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Compacts the block and transaction database by resaving the database file without
        /// all the deleted references.
        /// </summary>
        /// <param name="consensusTip">The current tip of consensus.</param>
        /// <param name="network">The network the node is running on.</param>
        /// <param name="nodeInitializing">Indicates whether or not this method is called from node startup or not.</param>
        /// <returns>The awaited task.</returns>
        Task PruneDatabase(ChainedHeader consensusTip, Network network, bool nodeInitializing);

        /// <summary> 
        /// The lowest block height that the repository has.
        /// <para>
        /// This also indicated where the node has been pruned up to.
        /// </para>
        /// </summary>
        HashHeightPair PrunedTip { get; }

        /// <summary>
        /// Sets the pruned tip.
        /// <para> 
        /// It will be saved once the block database has been compacted on node initialization or shutdown.
        /// </para>
        /// </summary>
        /// <param name="tip">The tip to set.</param>
        void UpdatePrunedTip(ChainedHeader tip);
    }
}
