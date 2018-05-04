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
        private Dictionary<uint256, ChainedHeader> blocksById = new Dictionary<uint256, ChainedHeader>();
        private Dictionary<int, ChainedHeader> blocksByHeight = new Dictionary<int, ChainedHeader>();
        private ReaderWriterLock lockObject = new ReaderWriterLock();

        private volatile ChainedHeader tip;
        private Network network;
        public override ChainedHeader Tip { get { return this.tip; } }
        public override int Height { get { return this.Tip.Height; } }
        public override Network Network { get { return this.network; } }
        
        public ConcurrentChain()
        {
            this.network = Network.Main;
        }
        
        public ConcurrentChain(BlockHeader genesisHeader, Network network = null) // TODO: Remove the null default
        {
            this.network = network ?? Network.Main;
            this.SetTip(new ChainedHeader(genesisHeader, genesisHeader.GetHash(), 0));
        }

        public ConcurrentChain(Network network)
            :this(network.GetGenesis().Header, network)
        {
        }

        public ConcurrentChain(byte[] bytes, Network network = null) // TODO: Remove the null default
            : this(network ?? Network.Main)
        {
            this.Load(bytes);
        }

        public void Load(byte[] chain)
        {
            this.Load(new MemoryStream(chain));
        }

        public void Load(Stream stream)
        {
            this.Load(new BitcoinStream(stream, false));
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
                            this.SetTipLocked(new ChainedHeader(header, header.GetHash(), 0));
                        }
                        else if (this.tip.HashBlock == header.HashPrevBlock && !(header.IsNull && header.Nonce == 0))
                            this.SetTipLocked(new ChainedHeader(header, id.Value, this.Tip));
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
            MemoryStream ms = new MemoryStream();
            this.WriteTo(ms);
            return ms.ToArray();
        }

        public void WriteTo(Stream stream)
        {
            this.WriteTo(new BitcoinStream(stream, true));
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

        public ConcurrentChain Clone()
        {
            ConcurrentChain chain = new ConcurrentChain();
            chain.network = this.network;
            chain.tip = this.tip;
            using (this.lockObject.LockRead())
            {
                foreach (KeyValuePair<uint256, ChainedHeader> kv in this.blocksById)
                {
                    chain.blocksById.Add(kv.Key, kv.Value);
                }

                foreach (KeyValuePair<int, ChainedHeader> kv in this.blocksByHeight)
                {
                    chain.blocksByHeight.Add(kv.Key, kv.Value);
                }
            }
            return chain;
        }

        /// <inheritdoc />
        public override ChainedHeader SetTip(ChainedHeader block)
        {
            using (this.lockObject.LockWrite())
            {
                return this.SetTipLocked(block);
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
                    this.SetTipLocked(block);
                    return true;
                }
            }

            return false;
        }

        private ChainedHeader SetTipLocked(ChainedHeader block)
        {
            int height = this.Tip == null ? -1 : this.Tip.Height;
            foreach (ChainedHeader orphaned in this.EnumerateThisToFork(block))
            {
                this.blocksById.Remove(orphaned.HashBlock);
                this.blocksByHeight.Remove(orphaned.Height);
                height--;
            }

            ChainedHeader fork = this.GetBlockLocked(height);
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
                if (object.ReferenceEquals(null, block) || object.ReferenceEquals(null, tip))
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
                return this.GetBlockLocked(height);
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
                    block = this.GetBlockLocked(i);
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