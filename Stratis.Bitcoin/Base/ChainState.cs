using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using System;
using System.Collections.Concurrent;

namespace Stratis.Bitcoin.Base
{
    public class ChainState
    {
        /// <summary>The fullnode interface.</summary>
        private readonly IFullNode fullNode;

        /// <summary>The last time the <see cref="ibdlastResult"/> was updated.</summary>
        private long ibdlastUpdate;
        
        /// <summary>A cached result of the IBD method.</summary>
        private bool ibdlastResult;

        /// <summary>A collection of blocks that have been found to be invalid.</summary>
        internal ConcurrentDictionary<uint256, uint256> InvalidBlocks;

        /// <summary>A provider of the date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>ChainBehaviors sharing this state will not broadcast headers which are above HighestValidatedPoW.</summary>
        public ChainedBlock HighestValidatedPoW { get; set; }

        public ChainState(IFullNode fullNode)
        {
            this.fullNode = fullNode;
            this.dateTimeProvider = this.fullNode.NodeService<IDateTimeProvider>(true);
            this.InvalidBlocks = new ConcurrentDictionary<uint256, uint256>();
        }

        /// <summary>
        /// Check if a block is marked as invalid.
        /// </summary>
        /// <param name="hashBlock">The block hash to check.</param>
        /// <returns>True if the block is marked as invalid.</returns>
        public bool IsMarkedInvalid(uint256 hashBlock)
        {
            return this.InvalidBlocks.ContainsKey(hashBlock);
        }

        /// <summary>
        /// Mark blocks as invalid to be processed by the node, this is used to prevent DOS attacks.
        /// </summary>
        /// <param name="hashBlock">The block hash to mark as invalid.</param>
        public void MarkBlockInvalid(uint256 hashBlock)
        {
            this.InvalidBlocks.TryAdd(hashBlock, hashBlock);
        }

        /// <summary>
        /// This method will check if the node is in a state of IBD (Initial Block Download)
        /// </summary>
        public bool IsInitialBlockDownload
        {
            get
            {
                if (this.ibdlastUpdate < this.dateTimeProvider?.GetUtcNow().Ticks)
                {
                    // Sample every minute.
                    this.ibdlastUpdate = this.dateTimeProvider.GetUtcNow().AddMinutes(1).Ticks;

                    // If consensus is not present IBD has no meaning. Set to false to match legacy code.                    
                    var IBDStateProvider = this.fullNode.NodeService<IBlockDownloadState>(true); 
                    this.ibdlastResult = IBDStateProvider == null ? false : IBDStateProvider.IsInitialBlockDownload();
                }
                return this.ibdlastResult;
            }
        }

        // For testing to be able to move the IBD.
        public void SetIsInitialBlockDownload(bool val, DateTime time)
        {
            this.ibdlastUpdate = time.Ticks;
            this.ibdlastResult = val;
        }
    }
}