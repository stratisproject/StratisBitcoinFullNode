using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// An indexer of the consensus chain.
    /// </summary>
    public class BestChainIndexer
    {
        private readonly IConsensusManager consensusManager;
        private readonly Dictionary<int, ChainedHeader> blocksByHeight = new Dictionary<int, ChainedHeader>();
        private readonly object lockObj = new object();

        internal BestChainIndexer(IConsensusManager consensusManager)
        {
            this.consensusManager = consensusManager;
        }

        public ChainedHeader Tip => this.consensusManager.Tip;

        internal void Initialize(ChainedHeader chainedHeader)
        {
            while (chainedHeader != null)
            {
                this.blocksByHeight.Add(chainedHeader.Height, chainedHeader);
                chainedHeader = chainedHeader.Previous;
            }
        }

        /// <summary>
        /// Add a new header to the best chain.
        /// </summary>
        /// <remarks>The method should only be set from <see cref="IConsensusManager"/>.</remarks>
        /// <param name="tipToAdd">The new chained header, either a new tip or a rewind of the tip.</param>
        internal void AddTip(ChainedHeader tipToAdd)
        {
            lock (this.lockObj)
            {
                // A tip was added.
                this.blocksByHeight.Add(tipToAdd.Height, tipToAdd);
            }
        }

        /// <summary>
        /// Remove a new header from the best chain.
        /// </summary>
        /// <remarks>The method should only be set from <see cref="IConsensusManager"/>.</remarks>
        /// <param name="newTip">The new chained header, either a new tip or a rewind of the tip.</param>
        internal void RemoveTip(ChainedHeader tipToRemove)
        {
            lock (this.lockObj)
            {
                // Reorged block.
                this.blocksByHeight.Remove(tipToRemove.Height);
            }
        }

        /// <summary>
        /// Gets the chained block header given a block ID (hash).
        /// </summary>
        /// <param name="blockHash">Block hash to retrieve.</param>
        /// <returns>The chained block header.</returns>
        public ChainedHeader GetBlock(uint256 blockHash)
        {
            return this.consensusManager.GetBlockDataAsync(blockHash).GetAwaiter().GetResult()?.ChainedHeader;
        }

        public ChainedHeader GetBlock(int height)
        {
            ChainedHeader result;

            lock (this.lockObj)
            {
                this.blocksByHeight.TryGetValue(height, out result);
            }

            return result;
        }

        /// <summary>
        /// Enumerate chain block headers after given block hash to genesis block.
        /// </summary>
        /// <param name="blockHash">Block hash to enumerate after.</param>
        /// <returns>Enumeration of chained block headers after given block hash.</returns>
        public IEnumerable<ChainedHeader> EnumerateAfter(uint256 blockHash)
        {
            ChainedHeader block = this.GetBlock(blockHash);

            if (block == null)
                return new ChainedHeader[0];

            return this.EnumerateAfter(block);
        }

        /// <summary>
        /// Enumerates chain block headers from the given chained block header to tip.
        /// </summary>
        /// <param name="block">Chained block header to enumerate from.</param>
        /// <returns>Enumeration of chained block headers from given chained block header to tip.</returns>
        public IEnumerable<ChainedHeader> EnumerateToTip(ChainedHeader block)
        {
            if (block == null)
                throw new ArgumentNullException("block");

            return this.EnumerateToTip(block.HashBlock);
        }

        /// <summary>
        /// Enumerates chain block headers from given block hash to tip.
        /// </summary>
        /// <param name="blockHash">Block hash to enumerate from.</param>
        /// <returns>Enumeration of chained block headers from the given block hash to tip.</returns>
        public IEnumerable<ChainedHeader> EnumerateToTip(uint256 blockHash)
        {
            ChainedHeader block = this.GetBlock(blockHash);
            if (block == null)
                yield break;

            yield return block;

            foreach (ChainedHeader chainedBlock in this.EnumerateAfter(blockHash))
                yield return chainedBlock;
        }

        /// <summary>
        /// Enumerates chain block headers after the given chained block header to genesis block.
        /// </summary>
        /// <param name="block">The chained block header to enumerate after.</param>
        /// <returns>Enumeration of chained block headers after the given block.</returns>
        public virtual IEnumerable<ChainedHeader> EnumerateAfter(ChainedHeader block)
        {
            int i = block.Height + 1;
            ChainedHeader prev = block;

            while (true)
            {
                ChainedHeader b = this.GetBlock(i);
                if ((b == null) || (b.Previous != prev))
                    yield break;

                yield return b;
                i++;
                prev = b;
            }
        }
    }
}