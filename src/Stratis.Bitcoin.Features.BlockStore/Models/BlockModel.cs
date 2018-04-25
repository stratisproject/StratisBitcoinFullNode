using System;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public class BlockModel
    {
        public string Hash { get; set; }
        public int Size { get; set; }
        public int Version { get; set; }
        public string Bits { get; set; }
        public DateTimeOffset Time { get; set; }
        public string[] Transactions { get; set; }
        public double Difficulty { get; set; }
        public string MerkleRoot { get; set; }
        public string PreviousBlockHash { get; set; }
        public uint Nonce { get; set; }

        public static implicit operator BlockModel(Block block)
        {
            var blockModel = new BlockModel()
            {
                Hash = block.GetHash().ToString(),
                Size = block.ToBytes().Length,
                Version = block.Header.Version,
                Bits = block.Header.Bits.ToString(),
                Time = block.Header.BlockTime,
                Nonce = block.Header.Nonce,
                PreviousBlockHash = block.Header.HashPrevBlock.ToString(),
                MerkleRoot = block.Header.HashMerkleRoot.ToString(),
                Difficulty = block.Header.Bits.Difficulty,
                Transactions = block.Transactions.Select(t => t.GetHash().ToString()).ToArray()
            };
            return blockModel;
        }
    }

    internal static class BlockExtension
    {
        public static BlockModel ToBlockModel(this Block block)
        {
            return block;
        }
    }
}
