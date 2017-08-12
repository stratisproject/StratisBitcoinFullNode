using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Stratis.Bitcoin.Base
{
    public class ChainState
    {
        private readonly FullNode fullNode;

        public ChainState(FullNode fullNode)
        {
            this.fullNode = fullNode;
        }

        internal ReaderWriterLockSlim _InvalidBlocksLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        internal HashSet<uint256> _InvalidBlocks = new HashSet<uint256>();

        public bool IsMarkedInvalid(uint256 hashBlock)
        {
            try
            {
                this._InvalidBlocksLock.EnterReadLock();
                return this._InvalidBlocks.Contains(hashBlock);
            }
            finally
            {
                this._InvalidBlocksLock.ExitReadLock();
            }
        }

        public void MarkBlockInvalid(uint256 blockHash)
        {
            try
            {
                this._InvalidBlocksLock.EnterWriteLock();
                this._InvalidBlocks.Add(blockHash);
            }
            finally
            {
                this._InvalidBlocksLock.ExitWriteLock();
            }
        }

        private long lastupdate;
        private bool lastresult;
        public bool IsInitialBlockDownload
        {
            get
            {
                if (this.lastupdate < this.fullNode.DateTimeProvider.GetUtcNow().Ticks)
                {
                    this.lastupdate = this.fullNode.DateTimeProvider.GetUtcNow().AddMinutes(1).Ticks; // sample every minute
                    this.lastresult = this.fullNode.IsInitialBlockDownload();
                }
                return this.lastresult;
            }
        }

        // for testing to be able to move the IBD
        public void SetIsInitialBlockDownload(bool val, DateTime time)
        {
            this.lastupdate = time.Ticks;
            this.lastresult = val;
        }

        /// <summary>
        /// ChainBehaviors sharing this state will not broadcast headers which are above HighestValidatedPoW
        /// </summary>
        public ChainedBlock HighestValidatedPoW
        {
            get; set;
        }

        /// <summary>
        /// Represents the last block stored to disk
        /// </summary>
        public ChainedBlock HighestPersistedBlock
        {
            get; set;
        }

        /// <summary>
        /// Represents the last block stored to the index repository
        /// </summary>
        public ChainedBlock HighestIndexedBlock
        {
            get; set;
        }
    }
}