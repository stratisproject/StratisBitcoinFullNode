using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Base
{
    public class ChainState
    {
        private readonly FullNode fullNode;

        private long lastUpdate;
        private bool lastResult;

        internal ReaderWriterLockSlim invalidBlocksLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        internal HashSet<uint256> invalidBlocks = new HashSet<uint256>();

        /// <summary>ChainBehaviors sharing this state will not broadcast headers which are above HighestValidatedPoW.</summary>
        public ChainedBlock HighestValidatedPoW { get; set; }

        public ChainState(FullNode fullNode)
        {
            this.fullNode = fullNode;
        }

        public bool IsMarkedInvalid(uint256 hashBlock)
        {
            try
            {
                this.invalidBlocksLock.EnterReadLock();
                return this.invalidBlocks.Contains(hashBlock);
            }
            finally
            {
                this.invalidBlocksLock.ExitReadLock();
            }
        }

        public void MarkBlockInvalid(uint256 blockHash)
        {
            try
            {
                this.invalidBlocksLock.EnterWriteLock();
                this.invalidBlocks.Add(blockHash);
            }
            finally
            {
                this.invalidBlocksLock.ExitWriteLock();
            }
        }

        public bool IsInitialBlockDownload
        {
            get
            {
                if (this.lastUpdate < this.fullNode.DateTimeProvider.GetUtcNow().Ticks)
                {
                    // Sample every minute.
                    this.lastUpdate = this.fullNode.DateTimeProvider.GetUtcNow().AddMinutes(1).Ticks;

                    // If consensus is not present IBD has no meaning. Set to false to match legacy code.                    
                    var IBDStateProvider = this.fullNode.NodeService<IBlockDownloadState>(true); 
                    this.lastResult = IBDStateProvider == null ? false : IBDStateProvider.IsInitialBlockDownload();
                }
                return this.lastResult;
            }
        }

        // For testing to be able to move the IBD.
        public void SetIsInitialBlockDownload(bool val, DateTime time)
        {
            this.lastUpdate = time.Ticks;
            this.lastResult = val;
        }
    }
}