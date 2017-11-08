using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace NBitcoin
{
    /// <summary>
    /// Thread safe class representing a chain of headers from genesis.
    /// </summary>
    public class ConcurrentChain : ChainBase
    {
        private Dictionary<uint256, ChainedBlock> blocksById = new Dictionary<uint256, ChainedBlock>();
        private Dictionary<int, ChainedBlock> blocksByHeight = new Dictionary<int, ChainedBlock>();
        private ReaderWriterLock lockObject = new ReaderWriterLock();

        private volatile ChainedBlock tip;
        public override ChainedBlock Tip { get { return this.tip; } }

        public override int Height { get { return this.Tip.Height; } }

        public ConcurrentChain()
        {
        }

        public ConcurrentChain(BlockHeader genesis)
        {
            this.SetTip(new ChainedBlock(genesis, 0));
        }

        public ConcurrentChain(Network network)
        {
            if (network != null)
            {
                Block genesis = network.GetGenesis();
                this.SetTip(new ChainedBlock(genesis.Header, 0));
            }
        }

        public ConcurrentChain(byte[] bytes)
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
                            this.SetTipLocked(new ChainedBlock(header, 0));
                        }
                        else this.SetTipLocked(new ChainedBlock(header, id.Value, this.Tip));

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
            using (this.lockObject.LockRead())
            {
                for (int i = 0; i < this.Tip.Height + 1; i++)
                {
                    ChainedBlock block = GetBlockLocked(i);
                    stream.ReadWrite(block.HashBlock.AsBitcoinSerializable());
                    stream.ReadWrite(block.Header);
                }
            }
        }

        public ConcurrentChain Clone()
        {
            ConcurrentChain chain = new ConcurrentChain();
            chain.tip = this.tip;
            using (this.lockObject.LockRead())
            {
                foreach (KeyValuePair<uint256, ChainedBlock> kv in this.blocksById)
                {
                    chain.blocksById.Add(kv.Key, kv.Value);
                }

                foreach (KeyValuePair<int, ChainedBlock> kv in this.blocksByHeight)
                {
                    chain.blocksByHeight.Add(kv.Key, kv.Value);
                }
            }
            return chain;
        }

        /// <inheritdoc />
        public override ChainedBlock SetTip(ChainedBlock block)
        {
            using (this.lockObject.LockWrite())
            {
                return this.SetTipLocked(block);
            }
        }

        private ChainedBlock SetTipLocked(ChainedBlock block)
        {
            int height = this.Tip == null ? -1 : this.Tip.Height;
            foreach (ChainedBlock orphaned in this.EnumerateThisToFork(block))
            {
                this.blocksById.Remove(orphaned.HashBlock);
                this.blocksByHeight.Remove(orphaned.Height);
                height--;
            }

            ChainedBlock fork = this.GetBlockLocked(height);
            foreach (ChainedBlock newBlock in block.EnumerateToGenesis().TakeWhile(c => c != fork))
            {
                this.blocksById.AddOrReplace(newBlock.HashBlock, newBlock);
                this.blocksByHeight.AddOrReplace(newBlock.Height, newBlock);
            }

            this.tip = block;
            return fork;
        }

        private IEnumerable<ChainedBlock> EnumerateThisToFork(ChainedBlock block)
        {
            if (this.tip == null)
                yield break;

            ChainedBlock tip = this.tip;
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

        public override ChainedBlock GetBlock(uint256 id)
        {
            using (this.lockObject.LockRead())
            {
                ChainedBlock result;
                this.blocksById.TryGetValue(id, out result);
                return result;
            }
        }

        private ChainedBlock GetBlockLocked(int height)
        {
            ChainedBlock result;
            this.blocksByHeight.TryGetValue(height, out result);
            return result;
        }

        public override ChainedBlock GetBlock(int height)
        {
            using (this.lockObject.LockRead())
            {
                return this.GetBlockLocked(height);
            }
        }

        #endregion

        protected override IEnumerable<ChainedBlock> EnumerateFromStart()
        {
            int i = 0;
            ChainedBlock block = null;
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

    internal class ReaderWriterLock
    {
        ReaderWriterLockSlim lockObject = new ReaderWriterLockSlim();

        public IDisposable LockRead()
        {
            return new ActionDisposable(() => this.lockObject.EnterReadLock(), () => this.lockObject.ExitReadLock());
        }
        public IDisposable LockWrite()
        {
            return new ActionDisposable(() => this.lockObject.EnterWriteLock(), () => this.lockObject.ExitWriteLock());
        }

        internal bool TryLockWrite(out IDisposable locked)
        {
            locked = null;
            if (this.lockObject.TryEnterWriteLock(0))
            {
                locked = new ActionDisposable(() =>
                {
                }, () => this.lockObject.ExitWriteLock());
                return true;
            }

            return false;
        }
    }
}