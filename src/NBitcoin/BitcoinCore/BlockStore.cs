#if !NOFILEIO
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NBitcoin.BitcoinCore
{
    public class BlockStore : Store<StoredBlock, Block>
    {
        public const int MAX_BLOCKFILE_SIZE = 0x8000000; // 128 MiB

        private bool headerOnly;

        public BlockStore(string folder, Network network)
            : base(folder, network)
        {
            this.MaxFileSize = MAX_BLOCKFILE_SIZE;
            this.FilePrefix = "blk";
        }

        public ConcurrentChain GetChain()
        {
            var chain = new ConcurrentChain(this.Network);
            SynchronizeChain(chain);
            return chain;
        }

        public void SynchronizeChain(ChainBase chain)
        {
            var headers = new Dictionary<uint256, BlockHeader>();
            var inChain = new HashSet<uint256>();

            inChain.Add(chain.GetBlock(0).HashBlock);

            foreach(BlockHeader header in Enumerate(true).Select(b => b.Item.Header))
            {
                uint256 hash = header.GetHash(this.Network.NetworkOptions);
                headers.Add(hash, header);
            }

            var toRemove = new List<uint256>();

            while (headers.Any())
            {
                foreach (KeyValuePair<uint256, BlockHeader> header in headers)
                {
                    if (inChain.Contains(header.Value.HashPrevBlock))
                    {
                        toRemove.Add(header.Key);
                        chain.SetTip(header.Value);
                        inChain.Add(header.Key);
                    }
                }

                foreach (uint256 item in toRemove)
                    headers.Remove(item);

                if (!toRemove.Any())
                    break;

                toRemove.Clear();
            }
        }

        public ConcurrentChain GetStratisChain()
        {
            var chain = new ConcurrentChain(this.Network);
            SynchronizeStratisChain(chain);
            return chain;
        }

        public void SynchronizeStratisChain(ChainBase chain)
        {
            var blocks = new Dictionary<uint256, Block>();
            var chainedBlocks = new Dictionary<uint256, ChainedBlock>();
            var inChain = new HashSet<uint256>();

            inChain.Add(chain.GetBlock(0).HashBlock);

            chainedBlocks.Add(chain.GetBlock(0).HashBlock, chain.GetBlock(0));

            foreach (Block block in this.Enumerate(false).Select(b => b.Item))
            {
                uint256 hash = block.GetHash();
                blocks.TryAdd(hash, block);
            }

            var toRemove = new List<uint256>();
            while (blocks.Any())
            {
                // to optimize keep a track of the last block
                ChainedBlock last = chain.GetBlock(0);

                foreach (KeyValuePair<uint256, Block> block in blocks)
                {
                    if (inChain.Contains(block.Value.Header.HashPrevBlock))
                    {
                        toRemove.Add(block.Key);

                        ChainedBlock chainedBlock;
                        if (last.HashBlock == block.Value.Header.HashPrevBlock)
                        {
                            chainedBlock = last;
                        }
                        else
                        {
                            if (!chainedBlocks.TryGetValue(block.Value.Header.HashPrevBlock, out chainedBlock))
                                break;
                        }

                        var chainedHeader = new ChainedBlock(block.Value.Header, block.Value.GetHash(this.Network.NetworkOptions), chainedBlock);
                        chain.SetTip(chainedHeader);
                        chainedBlocks.TryAdd(chainedHeader.HashBlock, chainedHeader);
                        inChain.Add(block.Key);
                        last = chainedHeader;
                    }
                }

                foreach (uint256 item in toRemove)
                    blocks.Remove(item);

                if (!toRemove.Any())
                    break;

                toRemove.Clear();
            }
        }

        // FIXME: this methods doesn't have a path to stop the recursion.
        public IEnumerable<StoredBlock> Enumerate(Stream stream, uint fileIndex = 0, DiskBlockPosRange range = null, bool headersOnly = false)
        {
            using (HeaderOnlyScope(headersOnly))
            {
                foreach (StoredBlock block in Enumerate(stream, fileIndex, range))
                {
                    yield return block;
                }
            }
        }

        private IDisposable HeaderOnlyScope(bool headersOnly)
        {
            var old = headersOnly;
            var oldBuff = this.BufferSize;

            return new Scope(() =>
            {
                this.headerOnly = headersOnly;

                if (!this.headerOnly)
                    this.BufferSize = 1024 * 1024;
            }, () =>
            {
                this.headerOnly = old;
                this.BufferSize = oldBuff;
            });
        }

        /// <summary>
        /// Enumerates "count" stored blocks starting at a given position.
        /// </summary>
        /// <param name="headersOnly"></param>
        /// <param name="blockCountStart">Inclusive block count</param>
        /// <param name="count">Exclusive block count</param>
        /// <returns></returns>
        public IEnumerable<StoredBlock> Enumerate(bool headersOnly, int blockCountStart, int count = 999999999)
        {
            int blockCount = 0;
            DiskBlockPos start = null;

            foreach(StoredBlock block in Enumerate(true, null))
            {
                if (blockCount == blockCountStart)
                    start = block.BlockPosition;

                blockCount++;
            }

            if (start == null)
                yield break;

            int i = 0;

            foreach (StoredBlock result in Enumerate(headersOnly, new DiskBlockPosRange(start)))
            {
                if (i >= count)
                    break;

                yield return result;

                i++;
            }
        }

        public IEnumerable<StoredBlock> Enumerate(bool headersOnly, DiskBlockPosRange range = null)
        {
            using (HeaderOnlyScope(headersOnly))
            {
                foreach (StoredBlock result in Enumerate(range))
                {
                    yield return result;
                }
            }
        }

        protected override StoredBlock ReadStoredItem(Stream stream, DiskBlockPos pos)
        {
            StoredBlock block = new StoredBlock(this.Network, pos);
            block.ParseSkipBlockContent = this.headerOnly;
            block.ReadWrite(stream, false);
            return block;
        }

        protected override StoredBlock CreateStoredItem(Block item, DiskBlockPos position)
        {
            return new StoredBlock(this.Network.Magic, item, position);
        }
    }
}
#endif