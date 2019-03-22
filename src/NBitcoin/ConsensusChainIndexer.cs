﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NBitcoin
{
    public class ConsensusChainIndexer
    {
        private readonly object lockObject = new object();
        private readonly Dictionary<int, ChainedHeader> blocksByHeight = new Dictionary<int, ChainedHeader>();
        private readonly Dictionary<uint256, ChainedHeader> blocksById = new Dictionary<uint256, ChainedHeader>();

        public Network Network { get; }
        public ChainedHeader Tip { get; private set; }

        public int Height => this.Tip.Height;
        public ChainedHeader Genesis => this.GetBlock(0);

        public ConsensusChainIndexer(Network network)
        {
            this.Network = network;

            this.Initialize(new ChainedHeader(network.GetGenesis().Header, network.GetGenesis().GetHash(), 0));
        }

        public ConsensusChainIndexer(Network network, ChainedHeader chainedHeader) 
        {
            this.Network = network;

            this.Initialize(chainedHeader);
        }

        public void Initialize(ChainedHeader chainedHeader)
        {
            this.blocksById.Clear();

            lock (this.lockObject)
            {
                ChainedHeader iterator = chainedHeader;

                while (iterator != null)
                {
                    this.blocksById.Add(chainedHeader.HashBlock, chainedHeader);
                    this.blocksByHeight.Add(chainedHeader.Height, chainedHeader);

                    if (chainedHeader.Height == 0)
                    {
                        if (chainedHeader.HashBlock != iterator.HashBlock)
                            throw new InvalidOperationException("Wrong network");
                    }

                    iterator = iterator.Previous;
                }

                this.Tip = chainedHeader;
            }
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
                ChainedHeader mi = this.GetBlock(hash);
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

            return this.FindFork(locator.Blocks);
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

        public void Add(ChainedHeader addTip)
        {
            lock (this.lockObject)
            {
                if(this.Tip.HashBlock != addTip.Previous.HashBlock)
                    throw new InvalidOperationException("New tip must be consecutive");

                this.blocksById.Add(addTip.HashBlock, addTip);
                this.blocksByHeight.Add(addTip.Height, addTip);

                this.Tip = addTip;
            }
        }

        public void Remove(ChainedHeader removeTip)
        {
            lock (this.lockObject)
            {
                if (this.Tip.HashBlock != removeTip.HashBlock)
                    throw new InvalidOperationException("Trying to remove item that is not the tip.");

                this.blocksById.Remove(removeTip.HashBlock);
                this.blocksByHeight.Remove(removeTip.Height);

                this.Tip = this.blocksById.Last().Value;
            }
        }

        public ChainedHeader GetBlock(uint256 id)
        {
            lock (this.lockObject)
            {
                ChainedHeader result;
                this.blocksById.TryGetValue(id, out result);
                return result;
            }
        }

        public ChainedHeader GetBlock(int height)
        {
            lock (this.lockObject)
            {
                ChainedHeader result;
                this.blocksByHeight.TryGetValue(height, out result);
                return result;
            }
        }

        public override string ToString()
        {
            return this.Tip == null ? "no tip" : this.Tip.ToString();
        }
    }
}