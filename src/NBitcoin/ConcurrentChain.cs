using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NBitcoin
{
    /// <summary>
    /// Thread safe class representing a chain of headers from genesis.
    /// </summary>
    public class ConcurrentChain : ChainBase
    {
        private readonly Dictionary<uint256, ChainedHeader> blocksById = new Dictionary<uint256, ChainedHeader>();
        private readonly Dictionary<int, ChainedHeader> blocksByHeight = new Dictionary<int, ChainedHeader>();
        private readonly ReaderWriterLock lockObject = new ReaderWriterLock();

        private volatile ChainedHeader tip;
        public override ChainedHeader Tip { get { return this.tip; } }

        public override int Height { get { return this.Tip.Height; } }

        private readonly Network network;
        public override Network Network { get { return this.network; } }

        [Obsolete("Do not use this constructor, it will eventually be replaced with ChainHeaderTree.")]
        public ConcurrentChain() { }

        public ConcurrentChain(Network network)
        {
            this.network = network;
            SetTip(new ChainedHeader(network.GetGenesis().Header, network.GetGenesis().GetHash(), 0));
        }

        public ConcurrentChain(Network network, ChainedHeader chainedHeader)
        {
            this.network = network;
            SetTip(chainedHeader);
        }

        public ConcurrentChain(Network network, byte[] bytes)
            : this(network)
        {
            Load(bytes);
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
        public override ChainedHeader SetTip(ChainedHeader block)
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

        public override ChainedHeader GetBlock(uint256 id)
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

        public override ChainedHeader GetBlock(int height)
        {
            using (this.lockObject.LockRead())
            {
                return GetBlockLocked(height);
            }
        }

        #endregion

        protected override IEnumerable<ChainedHeader> EnumerateFromStart()
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