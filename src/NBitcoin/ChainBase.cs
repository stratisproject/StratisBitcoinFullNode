using System;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin
{
    /// <summary>Base implementation for chains.</summary>
    public abstract class ChainBase
    {
        /// <summary>
        /// Gets the block given a block id(hash).
        /// </summary>
        /// <param name="id">Block id to retreive.</param>
        /// <returns>The block.</returns>
        public abstract ChainedBlock GetBlock(uint256 id);

        /// <summary>
        /// Gets the block at a given block height.
        /// </summary>
        /// <param name="height">Height to retreive block at.</param>
        /// <returns>The block.</returns>
        public abstract ChainedBlock GetBlock(int height);

        /// <summary>Gets the block at the tip of the chain.</summary>
        public abstract ChainedBlock Tip { get; }

        /// <summary>Gets the height of the chain.</summary>
        public abstract int Height { get; }

        /// <summary>
        /// Enumerates from start of the chain.
        /// </summary>
        /// <returns>An enumerable iterator.</returns>
        protected abstract IEnumerable<ChainedBlock> EnumerateFromStart();

        /// <summary>
        /// Force a new tip for the chain.
        /// </summary>
        /// <param name="chainedBlock">New tip for the chain.</param>
        /// <returns>Forking point.</returns>
        public abstract ChainedBlock SetTip(ChainedBlock chainedBlock);

        /// <summary>Gets the genesis block for the chain.</summary>
        public virtual ChainedBlock Genesis { get { return GetBlock(0); } }

        /// <summary>
        /// Gets an enumerable iterator for the chain.
        /// </summary>
        /// <param name="fromTip">Whether to iterate back from tip to find start or whether from start of chain.</param>
        /// <returns>An enumerable iterator for the chain.</returns>
        public IEnumerable<ChainedBlock> ToEnumerable(bool fromTip)
        {
            if (fromTip)
            {
                foreach (ChainedBlock block in this.Tip.EnumerateToGenesis())
                    yield return block;
            }
            else
            {
                foreach (ChainedBlock block in this.EnumerateFromStart())
                    yield return block;
            }
        }

        /// <summary>
        /// Sets the tip of this chain to the tip of another chain.
        /// </summary>
        /// <param name="otherChain">The other chain whose tip to apply to this chain.</param>
        /// <returns>The new tip.</returns>
        public ChainedBlock SetTip(ChainBase otherChain)
        {
            if (otherChain == null)
                throw new ArgumentNullException("otherChain");

            return this.SetTip(otherChain.Tip);
        }

        /// <summary>
        /// Sets the tip of this chain based upon another block header.
        /// </summary>
        /// <param name="header">The block header to set to tip.</param>
        /// <returns>Whether the tip was set successfully.</returns>
        public bool SetTip(BlockHeader header)
        {
            ChainedBlock chainedHeader;
            return this.TrySetTip(header, out chainedHeader);
        }

        /// <summary>
        /// Attempts to set the tip of this chain based upon another block header.
        /// </summary>
        /// <param name="header">The block header to set to tip.</param>
        /// <param name="chainedHeader">The newly chained block for the tip.</param>
        /// <returns>Whether the tip was set successfully.</returns>
        public bool TrySetTip(BlockHeader header, out ChainedBlock chainedHeader)
        {
            if (header == null)
                throw new ArgumentNullException("header");

            chainedHeader = null;
            ChainedBlock prev = this.GetBlock(header.HashPrevBlock);
            if (prev == null)
                return false;

            chainedHeader = new ChainedBlock(header, header.GetHash(), this.GetBlock(header.HashPrevBlock));
            this.SetTip(chainedHeader);
            return true;
        }

        /// <summary>
        /// Whether the chain contains a block with the given hash.
        /// </summary>
        /// <param name="hash">The hash to search for.</param>
        /// <returns>Whether the chain contains the block.</returns>
        public bool Contains(uint256 hash)
        {
            ChainedBlock block = GetBlock(hash);
            return block != null;
        }

        /// <summary>
        /// Whether the chain contains a block at same height as the given block.
        /// </summary>
        /// <param name="blockIndex">The block at the height to search.</param>
        /// <returns>Whether there is a block at the height of the given block.</returns>
        public bool Contains(ChainedBlock blockIndex)
        {
            if (blockIndex == null)
                throw new ArgumentNullException("blockIndex");

            return this.GetBlock(blockIndex.Height) != null;
        }

        /// <summary>
        /// Whether the given chain and this chain have the same tip.
        /// </summary>
        /// <param name="chain">The chain to compare this chain against.</param>
        /// <returns>Whether the tips of the chain have the same block hash.</returns>
        public bool SameTip(ChainBase chain)
        {
            if (chain == null)
                throw new ArgumentNullException("chain");

            return this.Tip.HashBlock == chain.Tip.HashBlock;
        }

        /// <summary>
        /// Gets the target work required for a network and a specific block height.
        /// </summary>
        /// <param name="network">The network to use for work computation.</param>
        /// <param name="height">The height of the block to get work for.</param>
        /// <returns>The target work.</returns>
        public Target GetWorkRequired(Network network, int height)
        {
            return this.GetBlock(height).GetWorkRequired(network);
        }

        /// <summary>
        /// Validates the chain on a given network.
        /// </summary>
        /// <param name="network">Network to validate the chain on.</param>
        /// <param name="fullChain">Whether to validate full chain (all blocks) or just the tip.</param>
        /// <returns>Whether the chain is valid or not.</returns>
        public bool Validate(Network network, bool fullChain = true)
        {
            ChainedBlock tip = this.Tip;
            if (tip == null)
                return false;

            if (!fullChain)
                return tip.Validate(network);

            foreach (ChainedBlock block in tip.EnumerateToGenesis())
            {
                if (!block.Validate(network))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the first common block between two chains.
        /// </summary>
        /// <param name="chain">The other chain.</param>
        /// <returns>First common block or <c>null</c>.</returns>
        public ChainedBlock FindFork(ChainBase chain)
        {
            if (chain == null)
                throw new ArgumentNullException("chain");

            return this.FindFork(chain.Tip.EnumerateToGenesis().Select(o => o.HashBlock));
        }

        /// <summary>
        /// Returns the first found block.
        /// </summary>
        /// <param name="hashes">Hash to search for.</param>
        /// <returns>First found block or <c>null</c>.</returns>
        public ChainedBlock FindFork(IEnumerable<uint256> hashes)
        {
            if (hashes == null)
                throw new ArgumentNullException("hashes");
            
            // Find the first block the caller has in the main chain.
            foreach (uint256 hash in hashes)
            {
                ChainedBlock mi = GetBlock(hash);
                if (mi != null)
                    return mi;
            }

            return null;
        }

        /// <summary>
        /// Finds the first block that exists in the chain from the block locator.
        /// </summary>
        /// <param name="locator">The block locator.</param>
        /// <returns>The first block that exists in the chain from the block locator.</returns>
        public ChainedBlock FindFork(BlockLocator locator)
        {
            if (locator == null)
                throw new ArgumentNullException("locator");

            return this.FindFork(locator.Blocks);
        }

        /// <summary>
        /// Enumerate chain after given block hash to genesis block.
        /// </summary>
        /// <param name="blockHash">Block hash to enumerate after.</param>
        /// <returns>Enumeration of chain after given block hash.</returns>
        public IEnumerable<ChainedBlock> EnumerateAfter(uint256 blockHash)
        {
            ChainedBlock block = this.GetBlock(blockHash);

            if (block == null)
                return new ChainedBlock[0];

            return this.EnumerateAfter(block);
        }

        /// <summary>
        /// Enumerates chain from the given block to tip.
        /// </summary>
        /// <param name="block">Block to enumerate from</param>
        /// <returns>Enumeration of chain from given block to tip.</returns>
        public IEnumerable<ChainedBlock> EnumerateToTip(ChainedBlock block)
        {
            if (block == null)
                throw new ArgumentNullException("block");

            return this.EnumerateToTip(block.HashBlock);
        }

        /// <summary>
        /// Enumerates chain from given block hash to tip.
        /// </summary>
        /// <param name="blockHash">Block hash to enumerate from.</param>
        /// <returns>Enumeration of chain from the given block hash to tip.</returns>
        public IEnumerable<ChainedBlock> EnumerateToTip(uint256 blockHash)
        {
            ChainedBlock block = this.GetBlock(blockHash);
            if (block == null)
                yield break;

            yield return block;

            foreach (ChainedBlock chainedBlock in this.EnumerateAfter(blockHash))
                yield return chainedBlock;
        }

        /// <summary>
        /// Enumerates chain after the given block to genesis block.
        /// </summary>
        /// <param name="block">The block to enumerate after.</param>
        /// <returns>Enumeration of chain after the given block.</returns>
        public virtual IEnumerable<ChainedBlock> EnumerateAfter(ChainedBlock block)
        {
            int i = block.Height + 1;
            ChainedBlock prev = block;

            while (true)
            {
                ChainedBlock b = GetBlock(i);
                if ((b == null) || (b.Previous != prev))
                    yield break;

                yield return b;
                i++;
                prev = b;
            }
        }
    }
}