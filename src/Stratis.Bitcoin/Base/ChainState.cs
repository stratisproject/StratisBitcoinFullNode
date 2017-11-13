using System;
using System.Collections.Concurrent;
using ConcurrentCollections;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Chain state holds various information related to the status of the chain and its validation.
    /// The data are provided by different components and the chaine state is a mechanism that allows 
    /// these components to share that data without creating extra dependencies.
    /// </summary>
    public class ChainState
    {
        /// <summary>The fullnode interface.</summary>
        private readonly IFullNode fullNode;

        /// <summary>The last time the <see cref="ibdLastResult"/> was updated.</summary>
        private long ibdLastUpdate;
        
        /// <summary>A cached result of the IBD method.</summary>
        private bool ibdLastResult;

        /// <summary>A collection of blocks that have been found to be invalid.</summary>
        private readonly ConcurrentHashSet<uint256> invalidBlocks;

        /// <summary>Collection of blocks that are to be considered invalid only for a certain amount of time.</summary>
        private readonly ConcurrentDictionary<uint256, DateTime> invalidBlocksWithExpiration;

        /// <summary>A provider of the date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>ChainBehaviors sharing this state will not broadcast headers which are above <see cref="ConsensusTip"/>.</summary>
        public ChainedBlock ConsensusTip { get; set; }

        /// <summary>Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.</summary>
        /// <remarks>TODO: This should be removed once consensus options are part of network.</remarks>
        public uint MaxReorgLength { get; set; }

        /// <summary>
        /// Initialize instance of the object.
        /// </summary>
        /// <param name="fullNode">The full node using this feature.</param>
        public ChainState(IFullNode fullNode)
        {
            this.fullNode = fullNode;
            this.dateTimeProvider = this.fullNode.NodeService<IDateTimeProvider>(true);
            this.invalidBlocks = new ConcurrentHashSet<uint256>();
            this.invalidBlocksWithExpiration = new ConcurrentDictionary<uint256, DateTime>();
        }

        /// <summary>
        /// Check if a block is marked as invalid.
        /// </summary>
        /// <param name="hashBlock">The block hash to check.</param>
        /// <returns><c>true</c> if the block is marked as invalid.</returns>
        public bool IsMarkedInvalid(uint256 hashBlock)
        {
            // First check if the block is permantently banned.
            bool res = this.invalidBlocks.Contains(hashBlock);
            if (!res)
            {
                // If it is not permantently banned, it could be temporarily banned,
                // so try to find expiration time of this ban, if it exists.
                DateTime expirationTime;
                if (this.invalidBlocksWithExpiration.TryGetValue(hashBlock, out expirationTime))
                {
                    // The block is invalid now if the expiration date is still in the future.
                    res = expirationTime > this.dateTimeProvider.GetUtcNow();

                    // If the expiration date is not in the future, remove the record from the list.
                    if (!res) this.invalidBlocksWithExpiration.TryRemove(hashBlock, out expirationTime);
                }
            }

            return res;
        }

        /// <summary>
        /// Mark blocks as invalid to be processed by the node, this is used to prevent DOS attacks.
        /// </summary>
        /// <param name="hashBlock">The block hash to mark as invalid.</param>
        /// <param name="rejectedUntil">Time in UTC after which the block is no longer considered as invalid, or <c>null</c> if the block is to be considered invalid forever.</param>
        public void MarkBlockInvalid(uint256 hashBlock, DateTime? rejectedUntil = null)
        {
            if (rejectedUntil != null) this.invalidBlocksWithExpiration.TryAdd(hashBlock, rejectedUntil.Value);
            else this.invalidBlocks.Add(hashBlock);
        }

        /// <summary>
        /// This method will check if the node is in a state of IBD (Initial Block Download)
        /// </summary>
        public bool IsInitialBlockDownload
        {
            get
            {
                if (this.ibdLastUpdate < this.dateTimeProvider?.GetUtcNow().Ticks)
                {
                    // Sample every minute.
                    this.ibdLastUpdate = this.dateTimeProvider.GetUtcNow().AddMinutes(1).Ticks;

                    // If consensus is not present IBD has no meaning. Set to false to match legacy code.                    
                    IBlockDownloadState IBDStateProvider = this.fullNode.NodeService<IBlockDownloadState>(true); 
                    this.ibdLastResult = IBDStateProvider == null ? false : IBDStateProvider.IsInitialBlockDownload();
                }

                return this.ibdLastResult;
            }
        }

        // For testing to be able to move the IBD.
        public void SetIsInitialBlockDownload(bool val, DateTime time)
        {
            this.ibdLastUpdate = time.Ticks;
            this.ibdLastResult = val;
        }
    }
}