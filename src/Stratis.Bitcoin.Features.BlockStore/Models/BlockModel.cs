using System;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public class BlockModel
    {
        public string Hash { get; set; }
        public int Size { get; set; }
        public int Version { get; set; }
        public string Bits { get; set; }
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset Time { get; set; }
        public string[] Transactions { get; set; }
        public double Difficulty { get; set; }
        public string MerkleRoot { get; set; }
        public string PreviousBlockHash { get; set; }
        public uint Nonce { get; set; }

        public BlockModel(Block block)
        {
            this.Hash = block.GetHash().ToString();
            this.Size = block.ToBytes().Length;
            this.Version = block.Header.Version;
            this.Bits = block.Header.Bits.ToCompact().ToString("x8");
            this.Time = block.Header.BlockTime;
            this.Nonce = block.Header.Nonce;
            this.PreviousBlockHash = block.Header.HashPrevBlock.ToString();
            this.MerkleRoot = block.Header.HashMerkleRoot.ToString();
            this.Difficulty = block.Header.Bits.Difficulty;
            this.Transactions = block.Transactions.Select(t => t.GetHash().ToString()).ToArray();
        }

        /// <summary>
        /// Creates a block model
        /// Used for deserializing from Json
        /// </summary>
        public BlockModel()
        {
        }
    }
}
