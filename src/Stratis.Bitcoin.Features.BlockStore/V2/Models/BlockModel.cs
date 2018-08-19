using System;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Controllers.Models;

namespace Stratis.Bitcoin.Features.BlockStore.V2.Models
{
    public class BlockModel
    {
        public string Hash { get; set; }
        public int Size { get; set; }
        public int Version { get; set; }
        public string Bits { get; set; }
        public DateTimeOffset Time { get; set; }

        public TransactionVerboseModel[] Transactions { get; set; }

        public double Difficulty { get; set; }
        public string MerkleRoot { get; set; }
        public string PreviousBlockHash { get; set; }
        public uint Nonce { get; set; }

        public int Height { get; set; }

        public  BlockModel(Block block, int height, Network network)
        {
            this.Height = height;
            this.Hash = block.GetHash().ToString();
            this.Size = block.ToBytes().Length;
            this.Version = block.Header.Version;
            this.Bits = block.Header.Bits.ToCompact().ToString("x8");
            this.Time = block.Header.BlockTime;
            this.Nonce = block.Header.Nonce;
            this.PreviousBlockHash = block.Header.HashPrevBlock.ToString();
            this.MerkleRoot = block.Header.HashMerkleRoot.ToString();
            this.Difficulty = block.Header.Bits.Difficulty;
            this.Transactions = block.Transactions.Select(trx => new TransactionVerboseModel(trx, network)).ToArray();
        }
    }

    
}
