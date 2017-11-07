using System;
using ConcurrentCollections;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Base
{
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

        /// <summary>A provider of the date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>ChainBehaviors sharing this state will not broadcast headers which are above <see cref="ConsensusTip"/>.</summary>
        public ChainedBlock ConsensusTip { get; set; }

        /// <summary>Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.</summary>
        /// <remarks>TODO: This should be removed once consensus options are part of network.</remarks>
        public uint MaxReorgLength { get; set; }

        public ChainState(IFullNode fullNode)
        {
            this.fullNode = fullNode;
            this.dateTimeProvider = this.fullNode.NodeService<IDateTimeProvider>(true);
            this.invalidBlocks = new ConcurrentHashSet<uint256>();
        }

        /// <summary>
        /// Check if a block is marked as invalid.
        /// </summary>
        /// <param name="hashBlock">The block hash to check.</param>
        /// <returns>True if the block is marked as invalid.</returns>
        public bool IsMarkedInvalid(uint256 hashBlock)
        {
            return this.invalidBlocks.Contains(hashBlock);
        }

        /// <summary>
        /// Mark blocks as invalid to be processed by the node, this is used to prevent DOS attacks.
        /// </summary>
        /// <param name="hashBlock">The block hash to mark as invalid.</param>
        public void MarkBlockInvalid(uint256 hashBlock)
        {
            this.invalidBlocks.Add(hashBlock);
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