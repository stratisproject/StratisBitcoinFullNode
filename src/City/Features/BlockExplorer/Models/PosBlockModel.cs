using System;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace City.Features.BlockExplorer.Models
{
    public class PosBlockModel
    {
        public PosBlockModel(Block block, ChainBase chain)
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
            this.Transactions = block.Transactions.Select(trx => new TransactionVerboseModel(trx, chain.Network)).ToArray();
            this.Height = chain.GetBlock(block.GetHash()).Height;
        }

        /// <summary>
        /// Creates a block model
        /// Used for deserializing from Json
        /// </summary>
        public PosBlockModel()
        {
        }

        [JsonProperty("hashproof")]
        public uint256 HashProof { get; set; }

        [JsonProperty("stakemodifierv2")]
        public uint256 StakeModifierV2 { get; set; }

        [JsonProperty("staketime")]
        public uint StakeTime { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; private set; }

        [JsonProperty("size")]
        public int Size { get; private set; }

        [JsonProperty("version")]
        public int Version { get; private set; }

        [JsonProperty("bits")]
        public string Bits { get; private set; }

        [JsonProperty("time")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset Time { get; private set; }

        [JsonProperty("tx")]
        public TransactionVerboseModel[] Transactions { get; set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; private set; }

        [JsonProperty("merkleroot")]
        public string MerkleRoot { get; private set; }

        [JsonProperty("previousblockhash")]
        public string PreviousBlockHash { get; private set; }

        [JsonProperty("nonce")]
        public uint Nonce { get; private set; }

        [JsonProperty("height")]
        public int Height { get; private set; }
    }
}
