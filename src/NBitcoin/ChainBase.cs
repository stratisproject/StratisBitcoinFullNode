using System;
using System.Collections.Generic;

namespace NBitcoin
{
    /// <summary>Base implementation for chains.</summary>
    public abstract class ChainBase
    {
        /// <summary>
        /// Gets the chained block header given a block ID (hash).
        /// </summary>
        /// <param name="id">Block ID to retreive.</param>
        /// <returns>The chained block header.</returns>
        public abstract ChainedHeader GetBlock(uint256 id);

        /// <summary>
        /// Gets the chained block header at a given block height.
        /// </summary>
        /// <param name="height">Height to retrieve chained block header at.</param>
        /// <returns>The chained block header.</returns>
        public abstract ChainedHeader GetBlock(int height);

        /// <summary>Gets the chained block header at the tip of the chain.</summary>
        public abstract ChainedHeader Tip { get; }

        /// <summary>The network associated with the chain.</summary>
        public abstract Network Network { get; }

        /// <summary>Gets the height of the chain.</summary>
        public abstract int Height { get; }

        /// <summary>
        /// Enumerates chained block headers from start of the chain.
        /// </summary>
        /// <returns>An enumerable iterator.</returns>
        protected abstract IEnumerable<ChainedHeader> EnumerateFromStart();

        /// <summary>
        /// Force a new tip for the chain.
        /// </summary>
        /// <param name="chainedHeader">New tip for the chain.</param>
        /// <returns>Forking point.</returns>
        public abstract ChainedHeader SetTip(ChainedHeader chainedHeader);

        /// <summary>Gets the genesis block for the chain.</summary>
        public virtual ChainedHeader Genesis { get { return GetBlock(0); } }

        /// <summary>
        /// Gets an enumerable iterator for the chain.
        /// </summary>
        /// <param name="fromTip">Whether to iterate back from tip to find start or whether from start of chain.</param>
        /// <returns>An enumerable iterator for the chain.</returns>
        public IEnumerable<ChainedHeader> ToEnumerable(bool fromTip)
        {
            if (fromTip)
            {
                foreach (ChainedHeader block in this.Tip.EnumerateToGenesis())
                    yield return block;
            }
            else
            {
                foreach (ChainedHeader block in EnumerateFromStart())
                    yield return block;
            }
        }

        /// <summary>
        /// Sets the tip of this chain to the tip of another chain.
        /// </summary>
        /// <param name="otherChain">The other chain whose tip to apply to this chain.</param>
        /// <returns>The new tip.</returns>
        public ChainedHeader SetTip(ChainBase otherChain)
        {
            if (otherChain == null)
                throw new ArgumentNullException("otherChain");

            return SetTip(otherChain.Tip);
        }

        /// <summary>
        /// Sets the tip of this chain based upon another block header.
        /// </summary>
        /// <param name="header">The block header to set to tip.</param>
        /// <returns>Whether the tip was set successfully.</returns>
        public bool SetTip(BlockHeader header)
        {
            ChainedHeader chainedHeader;
            return TrySetTip(header, out chainedHeader);
        }

        /// <summary>
        /// Attempts to set the tip of this chain based upon another block header.
        /// </summary>
        /// <param name="header">The block header to set to tip.</param>
        /// <param name="chainedHeader">The newly chained block header for the tip.</param>
        /// <returns>Whether the tip was set successfully. The method fails (and returns <c>false</c>)
        /// if the <paramref name="header"/>'s link to a previous header does not point to any block
        /// in the current chain.</returns>
        public bool TrySetTip(BlockHeader header, out ChainedHeader chainedHeader)
        {
            if (header == null)
                throw new ArgumentNullException("header");

            chainedHeader = null;
            ChainedHeader prev = GetBlock(header.HashPrevBlock);
            if (prev == null)
                return false;

            chainedHeader = new ChainedHeader(header, header.GetHash(), GetBlock(header.HashPrevBlock));
            SetTip(chainedHeader);
            return true;
        }

        /// <summary>
        /// Whether the chain contains a chained block header with the given hash.
        /// </summary>
        /// <param name="hash">The hash to search for.</param>
        /// <returns>Whether the chain contains the chained block header.</returns>
        public bool Contains(uint256 hash)
        {
            ChainedHeader block = GetBlock(hash);
            return block != null;
        }

        /// <summary>
        /// Whether the given chain and this chain have the same tip.
        /// </summary>
        /// <param name="chain">The chain to compare this chain against.</param>
        /// <returns>Whether the tips of the chains have the same block hash.</returns>
        public bool SameTip(ChainBase chain)
        {
            if (chain == null)
                throw new ArgumentNullException("chain");

            return this.Tip.HashBlock == chain.Tip.HashBlock;
        }

        /// <summary>
        /// Gets the required work for a network and a specific block height.
        /// </summary>
        /// <param name="network">The network to use for work computation.</param>
        /// <param name="height">The height of the block to get work for.</param>
        /// <returns>The required work.</returns>
        public Target GetWorkRequired(Network network, int height)
        {
            return GetBlock(height).GetWorkRequired(network);
        }

        /// <summary>
        /// Returns the first chained block header that exists in the chain from the list of block hashes.
        /// </summary>
        /// <param name="hashes">Hash to search for.</param>
        /// <returns>First found chained block header or <c>null</c> if not found.</returns>
        public ChainedHeader FindFork(IEnumerable<uint256> hashes)
        {
            if (hashes == null)
                throw new ArgumentNullException("hashes");
            
            // Find the first block the caller has in the main chain.
            foreach (uint256 hash in hashes)
            {
                ChainedHeader mi = GetBlock(hash);
                if (mi != null)
                    return mi;
            }

            return null;
        }

        /// <summary>
        /// Finds the first chained block header that exists in the chain from the block locator.
        /// </summary>
        /// <param name="locator">The block locator.</param>
        /// <returns>The first chained block header that exists in the chain from the block locator.</returns>
        public ChainedHeader FindFork(BlockLocator locator)
        {
            if (locator == null)
                throw new ArgumentNullException("locator");

            return FindFork(locator.Blocks);
        }

        /// <summary>
        /// Enumerate chain block headers after given block hash to genesis block.
        /// </summary>
        /// <param name="blockHash">Block hash to enumerate after.</param>
        /// <returns>Enumeration of chained block headers after given block hash.</returns>
        public IEnumerable<ChainedHeader> EnumerateAfter(uint256 blockHash)
        {
            ChainedHeader block = GetBlock(blockHash);

            if (block == null)
                return new ChainedHeader[0];

            return EnumerateAfter(block);
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

            return EnumerateToTip(block.HashBlock);
        }

        /// <summary>
        /// Enumerates chain block headers from given block hash to tip.
        /// </summary>
        /// <param name="blockHash">Block hash to enumerate from.</param>
        /// <returns>Enumeration of chained block headers from the given block hash to tip.</returns>
        public IEnumerable<ChainedHeader> EnumerateToTip(uint256 blockHash)
        {
            ChainedHeader block = GetBlock(blockHash);
            if (block == null)
                yield break;

            yield return block;

            foreach (ChainedHeader chainedBlock in EnumerateAfter(blockHash))
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
                ChainedHeader b = GetBlock(i);
                if ((b == null) || (b.Previous != prev))
                    yield break;

                yield return b;
                i++;
                prev = b;
            }
        }
    }
}