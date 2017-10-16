using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// An object that holds block stake information.
    /// </summary>
    public sealed class StakeItem
    {
        private StakeItem() { }

        /// <summary>The hash of the block.</summary>
        public uint256 BlockHash { get; private set; }

        /// <summary>The block stake.</summary>
        public BlockStake BlockStake { get; private set; }

        /// <summary>The height of the block.</summary>
        public long? Height { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the item exists in the store. It is used to determine
        /// if the <see cref="StakeChainStore"/> should persist the item or not. />
        /// </summary>
        public bool ExistsInStore { get; private set; }

        /// <summary>
        /// Sets the item's <see cref="ExistsInStore"/> value to <c>true</c>.
        /// </summary>
        internal void InStore()
        {
            this.ExistsInStore = true;
        }

        /// <summary>
        /// Sets the item's <see cref="BlockStake"/> value.
        /// </summary>
        internal void Update(BlockStake blockStake)
        {
            Guard.NotNull(blockStake, nameof(blockStake));

            this.BlockStake = blockStake;

            InStore();
        }

        /// <summary>
        /// Static method to create a valid <see cref="StakeItem"/> object.
        /// </summary>
        /// <param name="blockHash">The hash of the stake block.</param>
        /// <param name="blockStake">The block stake.</param>
        /// <param name="height">The height of the stake block.</param>
        /// <returns>Fully initialized <see cref="StakeItem"/></returns>
        internal static StakeItem Create(uint256 blockHash, BlockStake blockStake, long height)
        {
            Guard.NotNull(blockHash, nameof(blockHash));
            Guard.NotNull(blockStake, nameof(blockStake));

            var stakeItem = new StakeItem();
            stakeItem.BlockHash = blockHash;
            stakeItem.BlockStake = blockStake;
            stakeItem.Height = height;
            return stakeItem;
        }

        /// <summary>
        /// Static method to partially initialize <see cref="StakeItem"/> to query the stake chain store
        /// database with.
        /// </summary>
        /// <param name="blockHash">The hash of the stake block to query.</param>
        /// <param name="height">The height of the stake block to query.</param>
        /// <returns>Partially initialized <see cref="StakeItem"/>StakeItem</returns>
        internal static StakeItem Query(uint256 blockHash, int? height = null)
        {
            Guard.NotNull(blockHash, nameof(blockHash));

            var stakeItem = new StakeItem();
            stakeItem.BlockHash = blockHash;
            stakeItem.Height = height;
            return stakeItem;
        }
    }
}