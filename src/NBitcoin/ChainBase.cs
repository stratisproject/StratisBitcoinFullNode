using System;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin
{
    public abstract class ChainBase
    {
        public abstract ChainedBlock GetBlock(uint256 id);
        public abstract ChainedBlock GetBlock(int height);

        public abstract ChainedBlock Tip { get; }

        public abstract int Height { get; }

        protected abstract IEnumerable<ChainedBlock> EnumerateFromStart();

        /// <summary>
        /// Force a new tip for the chain.
        /// </summary>
        /// <param name="chainedBlock">New tip for the chain.</param>
        /// <returns>Forking point.</returns>
        public abstract ChainedBlock SetTip(ChainedBlock chainedBlock);

        public virtual ChainedBlock Genesis
        {
            get
            {
                return GetBlock(0);
            }
        }

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

        public ChainedBlock SetTip(ChainBase otherChain)
        {
            if (otherChain == null)
                throw new ArgumentNullException("otherChain");

            return this.SetTip(otherChain.Tip);
        }

        public bool SetTip(BlockHeader header)
        {
            ChainedBlock chainedHeader;
            return this.TrySetTip(header, out chainedHeader);
        }

        public bool TrySetTip(BlockHeader header, out ChainedBlock chainedHeader)
        {
            if (header == null)
                throw new ArgumentNullException("header");

            chainedHeader = null;
            ChainedBlock prev = GetBlock(header.HashPrevBlock);
            if (prev == null)
                return false;

            chainedHeader = new ChainedBlock(header, header.GetHash(), GetBlock(header.HashPrevBlock));
            this.SetTip(chainedHeader);
            return true;
        }

        public bool Contains(uint256 hash)
        {
            ChainedBlock block = GetBlock(hash);
            return block != null;
        }

        public bool Contains(ChainedBlock blockIndex)
        {
            if (blockIndex == null)
                throw new ArgumentNullException("blockIndex");

            return this.GetBlock(blockIndex.Height) != null;
        }

        public bool SameTip(ChainBase chain)
        {
            if (chain == null)
                throw new ArgumentNullException("chain");

            return this.Tip.HashBlock == chain.Tip.HashBlock;
        }

        public Target GetWorkRequired(Network network, int height)
        {
            return this.GetBlock(height).GetWorkRequired(network);
        }

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

        public ChainedBlock FindFork(BlockLocator locator)
        {
            if (locator == null)
                throw new ArgumentNullException("locator");

            return this.FindFork(locator.Blocks);
        }

        public IEnumerable<ChainedBlock> EnumerateAfter(uint256 blockHash)
        {
            ChainedBlock block = this.GetBlock(blockHash);

            if (block == null)
                return new ChainedBlock[0];

            return this.EnumerateAfter(block);
        }

        public IEnumerable<ChainedBlock> EnumerateToTip(ChainedBlock block)
        {
            if (block == null)
                throw new ArgumentNullException("block");

            return this.EnumerateToTip(block.HashBlock);
        }

        public IEnumerable<ChainedBlock> EnumerateToTip(uint256 blockHash)
        {
            ChainedBlock block = this.GetBlock(blockHash);
            if (block == null)
                yield break;

            yield return block;

            foreach (ChainedBlock chainedBlock in this.EnumerateAfter(blockHash))
                yield return chainedBlock;
        }

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