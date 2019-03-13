using System;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Controllers.Models
{
    public class BlockModel
    {
        [JsonProperty("hash")]
        public string Hash { get; private set; }

        [JsonProperty("confirmations")]
        public int Confirmations { get; private set; }

        [JsonProperty("size")]
        public int Size { get; private set; }

        [JsonProperty("weight")]
        public long Weight { get; private set; }

        [JsonProperty("height")]
        public int Height { get; private set; }

        [JsonProperty("version")]
        public int Version { get; private set; }

        [JsonProperty("versionhex")]
        public string VersionHex { get; private set; }

        [JsonProperty("merkleroot")]
        public string MerkleRoot { get; private set; }

        [JsonProperty("tx")]
        public string[] Transactions { get; private set; }

        [JsonProperty("time")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset Time { get; private set; }

        [JsonProperty("mediantime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset MedianTime { get; private set; }

        [JsonProperty("nonce")]
        public uint Nonce { get; private set; }

        [JsonProperty("bits")]
        public string Bits { get; private set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; private set; }

        [JsonProperty("chainwork")]
        public string ChainWork { get; private set; }

        [JsonProperty("ntx")]
        public int NumberOfTransactions { get; private set; }

        [JsonProperty("previousblockhash")]
        public string PreviousBlockHash { get; private set; }

        [JsonProperty("nextblockhash")]
        public string NextBlockHash { get; private set; }

        public BlockModel(Block block, ChainBase chain, IConsensus consensus)
        {
            this.Hash = block.GetHash().ToString();
            this.Confirmations = chain.Tip.Height - chain.GetBlock(block.GetHash()).Height + 1;
            this.Size = block.ToBytes().Length;
            this.Weight = block.GetBlockWeight(consensus);
            this.Height = chain.GetBlock(block.GetHash()).Height;
            this.Version = block.Header.Version;
            this.VersionHex = block.Header.Version.ToString("x8");
            this.MerkleRoot = block.Header.HashMerkleRoot.ToString();
            this.Transactions = block.Transactions.Select(t => t.GetHash().ToString()).ToArray();
            this.Time = block.Header.BlockTime;
            this.MedianTime = chain.GetBlock(block.GetHash()).GetMedianTimePast();
            this.Nonce = block.Header.Nonce;
            this.Bits = block.Header.Bits.ToCompact().ToString("x8");
            this.Difficulty = block.Header.Bits.Difficulty;
            this.ChainWork = chain.GetBlock(block.GetHash()).ChainWork.ToString();
            this.NumberOfTransactions = this.Transactions.Count();
            this.PreviousBlockHash = block.Header.HashPrevBlock.ToString();
            this.NextBlockHash = chain.GetBlock(block.GetHash()).Next?.First().HashBlock.ToString();
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
