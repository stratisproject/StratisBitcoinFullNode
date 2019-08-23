using System.IO;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using NBitcoin;

namespace Stratis.Features.SQLiteWalletRepository.Tests.TestHelpers
{
    public struct BlockPrefix
    {
        // This block's height.
        public int BlockHeight { get; set; }

        // Next block at this height or first block at next height.
        public int NextBlockPos { get; set; }

        // Exact previous block position.
        public int PrevBlockPos { get; set; }

        // Previous skip block position.
        public int PrevSkipBlockPos { get; set; }

        // Number of blocks at this height.
        public int NumBlocks { get; set; }
    }

    public struct BlockEntry
    {
        // This block's size.
        public int BlockSize { get; set; }

        // This block's hash.
        public uint256 BlockHash { get; set; }

        // This block's data.
        public byte[] BlockData { get; set; }
    }

    public class FastBlockStore
    {
        public string StoreFile { get; private set; }
        public Dictionary<uint256, BlockPrefix> Tips { get; private set; }

        public FastBlockStore(string storeName = "MyStore")
        {
            this.Tips = new Dictionary<uint256, BlockPrefix>();
            this.StoreFile = Path.Combine(Path.GetTempPath(), storeName);

        }

        public void Initialize()
        {
            // Create the memory-mapped file.
            if (!File.Exists(this.StoreFile))
            {
                int blockPrefixSize = Marshal.SizeOf(typeof(BlockPrefix));

                FileStream f = File.Create(this.StoreFile, blockPrefixSize);
                var buffer = new byte[blockPrefixSize];
                f.Write(buffer, 0, buffer.Length);
                f.Close();
            }

            long length = new FileInfo(this.StoreFile).Length;

            using (var mmf = MemoryMappedFile.CreateFromFile(this.StoreFile, FileMode.Open, "blockChain"))
            {
                using (var accessor = mmf.CreateViewAccessor(0, length))
                {
                    int blockPrefixSize = Marshal.SizeOf(typeof(BlockPrefix));
                    BlockPrefix blockPrefix;

                    long storeSize = accessor.Capacity;
                    accessor.Read(storeSize - blockPrefixSize, out blockPrefix);
                }
            }
        }
    }
}
