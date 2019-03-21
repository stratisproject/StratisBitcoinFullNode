using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NBitcoin
{
    /// <summary>
    /// Thread safe class representing a chain of headers from genesis.
    /// </summary>
    public class ConsensusChainIndexer
    {
        private readonly Dictionary<uint256, ChainedHeader> blocksById = new Dictionary<uint256, ChainedHeader>();
        private readonly Dictionary<int, ChainedHeader> blocksByHeight = new Dictionary<int, ChainedHeader>();
        private readonly ReaderWriterLock lockObject = new ReaderWriterLock();

        private volatile ChainedHeader tip;
        public ChainedHeader Tip { get { return this.tip; } }

        public  int Height { get { return this.Tip.Height; } }

        private readonly Network network;
        public Network Network { get { return this.network; } }

        [Obsolete("Do not use this constructor, it will eventually be replaced with ChainHeaderTree.")]
        public ConsensusChainIndexer() { }

        public ConsensusChainIndexer(Network network)
        {
            this.network = network;
            SetTip(new ChainedHeader(network.GetGenesis().Header, network.GetGenesis().GetHash(), 0));
        }

        public ConsensusChainIndexer(Network network, ChainedHeader chainedHeader)
        {
            this.network = network;
            SetTip(chainedHeader);
        }

        public ConsensusChainIndexer(Network network, byte[] bytes)
            : this(network)
        {
            Load(bytes);
        }

        /// <summary>Gets the genesis block for the chain.</summary>
        public virtual ChainedHeader Genesis { get { return GetBlock(0); } }

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

        public void Load(byte[] chain)
        {
            using (var ms = new MemoryStream(chain))
            {
                Load(ms);
            }
        }

        public void Load(Stream stream)
        {
            Load(new BitcoinStream(stream, false));
        }

        public void Load(BitcoinStream stream)
        {
            stream.ConsensusFactory = this.network.Consensus.ConsensusFactory;

            using (this.lockObject.LockWrite())
            {
                try
                {
                    int height = 0;
                    while (true)
                    {
                        uint256.MutableUint256 id = null;
                        stream.ReadWrite<uint256.MutableUint256>(ref id);
                        BlockHeader header = null;
                        stream.ReadWrite(ref header);
                        if (height == 0)
                        {
                            this.blocksByHeight.Clear();
                            this.blocksById.Clear();
                            this.tip = null;
                            SetTipLocked(new ChainedHeader(header, header.GetHash(), 0));
                        }
                        else if (this.tip.HashBlock == header.HashPrevBlock && !(header.IsNull && header.Nonce == 0))
                            SetTipLocked(new ChainedHeader(header, id.Value, this.Tip));
                        else
                            break;

                        height++;
                    }
                }
                catch (EndOfStreamException)
                {
                }
            }
        }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                WriteTo(ms);
                return ms.ToArray();
            }
        }

        public void WriteTo(Stream stream)
        {
            WriteTo(new BitcoinStream(stream, true));
        }

        public void WriteTo(BitcoinStream stream)
        {
            stream.ConsensusFactory = this.network.Consensus.ConsensusFactory;

            using (this.lockObject.LockRead())
            {
                for (int i = 0; i < this.Tip.Height + 1; i++)
                {
                    ChainedHeader block = GetBlockLocked(i);
                    stream.ReadWrite(block.HashBlock.AsBitcoinSerializable());
                    stream.ReadWrite(block.Header);
                }
            }
        }

        /// <inheritdoc />
        public ChainedHeader SetTip(ChainedHeader block)
        {
            using (this.lockObject.LockWrite())
            {
                return SetTipLocked(block);
            }
        }

        /// <summary>
        /// Sets the tip of this chain to the tip of another chain if it's chainwork is greater.
        /// </summary>
        /// <param name="block">Tip to set.</param>
        /// <returns><c>true</c> if the tip was set; <c>false</c> otherwise.</returns>
        public bool SetTipIfChainworkIsGreater(ChainedHeader block)
        {
            using (this.lockObject.LockWrite())
            {
                if ((this.tip == null) || (block.ChainWork > this.tip.ChainWork))
                {
                    SetTipLocked(block);
                    return true;
                }
            }

            return false;
        }

        private ChainedHeader SetTipLocked(ChainedHeader block)
        {
            int height = this.Tip == null ? -1 : this.Tip.Height;
            foreach (ChainedHeader orphaned in EnumerateThisToFork(block))
            {
                this.blocksById.Remove(orphaned.HashBlock);
                this.blocksByHeight.Remove(orphaned.Height);
                height--;
            }

            ChainedHeader fork = GetBlockLocked(height);
            foreach (ChainedHeader newBlock in block.EnumerateToGenesis().TakeWhile(c => c != fork))
            {
                this.blocksById.AddOrReplace(newBlock.HashBlock, newBlock);
                this.blocksByHeight.AddOrReplace(newBlock.Height, newBlock);
            }

            this.tip = block;
            return fork;
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

        private IEnumerable<ChainedHeader> EnumerateThisToFork(ChainedHeader block)
        {
            if (this.tip == null)
                yield break;

            ChainedHeader tip = this.tip;
            while (true)
            {
                if (ReferenceEquals(null, block) || ReferenceEquals(null, tip))
                    throw new InvalidOperationException("No fork found between the two chains");

                if (tip.Height > block.Height)
                {
                    yield return tip;
                    tip = tip.Previous;
                }
                else if (tip.Height < block.Height)
                {
                    block = block.Previous;
                }
                else if (tip.Height == block.Height)
                {
                    if (tip.HashBlock == block.HashBlock)
                        break;

                    yield return tip;

                    block = block.Previous;
                    tip = tip.Previous;
                }
            }
        }

        #region IChain Members

        public ChainedHeader GetBlock(uint256 id)
        {
            using (this.lockObject.LockRead())
            {
                ChainedHeader result;
                this.blocksById.TryGetValue(id, out result);
                return result;
            }
        }

        private ChainedHeader GetBlockLocked(int height)
        {
            ChainedHeader result;
            this.blocksByHeight.TryGetValue(height, out result);
            return result;
        }

        public ChainedHeader GetBlock(int height)
        {
            using (this.lockObject.LockRead())
            {
                return GetBlockLocked(height);
            }
        }

        #endregion

        protected IEnumerable<ChainedHeader> EnumerateFromStart()
        {
            int i = 0;
            ChainedHeader block = null;
            while (true)
            {
                using (this.lockObject.LockRead())
                {
                    block = GetBlockLocked(i);
                    if (block == null)
                        yield break;
                }

                yield return block;
                i++;
            }
        }

        public override string ToString()
        {
            return this.Tip == null ? "no tip" : this.Tip.Height.ToString();
        }
    }
}