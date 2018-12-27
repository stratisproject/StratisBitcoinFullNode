using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.Pruning
{
    /// <summary>
    /// Prunes and compacts the block store database by deleting blocks lower than a certain height and recreating the database file on disk.
    /// </summary>
    public interface IPrunedBlockRepository
    {
        /// <summary>
        /// Initializes the pruned block repository.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Prunes and compacts the block and transaction database.
        /// <para>
        /// The method first prunes by deleting blocks from the block store that are below the <see cref="StoreSettings.AmountOfBlocksToKeep"/> from the store tip.
        /// </para>
        /// <para>
        /// Once this is done the database is compacted by resaving the database file without the deleted references, reducing the file size on disk.
        /// </para>
        /// </summary>
        /// <param name="blockStoreTip">The current tip of the store.</param>
        /// <param name="network">The network the node is running on.</param>
        /// <param name="nodeInitializing">Indicates whether or not this method is called from node startup or not.</param>
        Task PruneAndCompactDatabase(ChainedHeader blockStoreTip, Network network, bool nodeInitializing);

        /// <summary> 
        /// The lowest block hash and height that the repository has.
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
