using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public class Block
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

        public static implicit operator Block(NBitcoin.Block block)
        {
            var blockModel = new Block()
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
        public static Block ToBlockModel(this NBitcoin.Block block)
        {
            return block;
        }
    }
}
