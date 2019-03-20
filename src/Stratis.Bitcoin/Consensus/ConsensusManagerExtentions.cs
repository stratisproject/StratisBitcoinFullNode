using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus
{
    public static class ConsensusManagerExtentions
    {
        /// <summary>
        /// Gets the chained block header given a block ID (hash).
        /// </summary>
        /// <param name="consensusManager">The consensus manager class.</param>
        /// <param name="blockHash">Block hash to retrieve.</param>
        /// <returns>The chained block header.</returns>
        public static ChainedHeader GetBlock(this IConsensusManager consensusManager, uint256 blockHash)
        {
            // TODO: delete this method once async is removed fom CM.
            return consensusManager.GetBlockDataAsync(blockHash).GetAwaiter().GetResult()?.ChainedHeader;
        }

        /// <summary>
        /// Returns the first chained block header that exists in the chain from the list of block hashes.
        /// </summary>
        /// <param name="consensusManager">The consensus manager class.</param>
        /// <param name="hashes">Hash to search for.</param>
        /// <returns>First found chained block header or <c>null</c> if not found.</returns>
        public static ChainedHeader FindFork(this IConsensusManager consensusManager, IEnumerable<uint256> hashes)
        {
            if (hashes == null)
                throw new ArgumentNullException("hashes");

            // Find the first block the caller has in the main chain.
            foreach (uint256 hash in hashes)
            {
                ChainedHeader mi = consensusManager.GetBlock(hash);
                if (mi != null)
                    return mi;
            }

            return null;
        }

        /// <summary>
        /// Finds the first chained block header that exists in the chain from the block locator.
        /// </summary>
        /// <param name="consensusManager">The consensus manager class.</param>
        /// <param name="locator">The block locator.</param>
        /// <returns>The first chained block header that exists in the chain from the block locator.</returns>
        public static ChainedHeader FindFork(this IConsensusManager consensusManager, BlockLocator locator)
        {
            if (locator == null)
                throw new ArgumentNullException("locator");

            return consensusManager.FindFork(locator.Blocks);
        }

        /// <summary>
        /// Enumerate chain block headers after given block hash to genesis block.
        /// </summary>
        /// <param name="consensusManager">The consensus manager class.</param>
        /// <param name="blockHash">Block hash to enumerate after.</param>
        /// <returns>Enumeration of chained block headers after given block hash.</returns>
        public static IEnumerable<ChainedHeader> EnumerateAfter(this IConsensusManager consensusManager, uint256 blockHash)
        {
            ChainedHeader block = consensusManager.GetBlock(blockHash);

            if (block == null)
                return new ChainedHeader[0];

            return consensusManager.EnumerateAfter(block);
        }

        /// <summary>
        /// Enumerates chain block headers from the given chained block header to tip.
        /// </summary>
        /// <param name="consensusManager">The consensus manager class.</param>
        /// <param name="block">Chained block header to enumerate from.</param>
        /// <returns>Enumeration of chained block headers from given chained block header to tip.</returns>
        public static IEnumerable<ChainedHeader> EnumerateToTip(this IConsensusManager consensusManager, ChainedHeader block)
        {
            if (block == null)
                throw new ArgumentNullException("block");

            return consensusManager.EnumerateToTip(block.HashBlock);
        }

        /// <summary>
        /// Enumerates chain block headers from given block hash to tip.
        /// </summary>
        /// <param name="consensusManager">The consensus manager class.</param>
        /// <param name="blockHash">Block hash to enumerate from.</param>
        /// <returns>Enumeration of chained block headers from the given block hash to tip.</returns>
        public static IEnumerable<ChainedHeader> EnumerateToTip(this IConsensusManager consensusManager, uint256 blockHash)
        {
            ChainedHeader block = consensusManager.GetBlock(blockHash);
            if (block == null)
                yield break;

            yield return block;

            foreach (ChainedHeader chainedBlock in consensusManager.EnumerateAfter(blockHash))
                yield return chainedBlock;
        }

        /// <summary>
        /// Enumerates chain block headers after the given chained block header to genesis block.
        /// </summary>
        /// <param name="consensusManager">The consensus manager class.</param>
        /// <param name="block">The chained block header to enumerate after.</param>
        /// <returns>Enumeration of chained block headers after the given block.</returns>
        public static IEnumerable<ChainedHeader> EnumerateAfter(this IConsensusManager consensusManager, ChainedHeader block)
        {
            int i = block.Height + 1;
            ChainedHeader prev = block;

            while (true)
            {
                ChainedHeader b = consensusManager.GetBlock(i);
                if ((b == null) || (b.Previous != prev))
                    yield break;

                yield return b;
                i++;
                prev = b;
            }
        }
    }
}