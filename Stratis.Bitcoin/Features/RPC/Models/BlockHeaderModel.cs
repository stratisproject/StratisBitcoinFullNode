using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public class BlockHeaderModel
    {
        public BlockHeaderModel(BlockHeader blockHeader)
        {           
            if (blockHeader != null)
            {
                this.version = (uint)blockHeader.Version;
                this.nonce = (int)blockHeader.Nonce;
                byte[] bytes = BitConverter.GetBytes(blockHeader.Bits.ToCompact());
                string encodedBits = Encoders.Hex.EncodeData(bytes);
                this.bits = encodedBits;
                this.previousblockhash = blockHeader.HashPrevBlock.ToString();
                this.time = blockHeader.Time;
                this.merkleroot = blockHeader.HashMerkleRoot.ToString();

                //TODO: Populate these fields
                // hash
                // confirmations
                // height
                // mediantime
                // difficulty
                // chainwork
                // nextblockhash
                
            }
        }

        [JsonProperty(Order = 3)]
        public uint version { get; set; }

        [JsonProperty(Order = 4)]
        public string merkleroot { get; set; }

        [JsonProperty(Order = 6)]
        public int nonce { get; set; }

        [JsonProperty(Order = 7)]
        public string bits { get; set; }

        [JsonProperty(Order = 10)]
        public string previousblockhash { get; set; }

        #region TODO:

        [JsonProperty(Order = 0)]
        public string hash { get; set; }

        [JsonProperty(Order = 1)]
        public int confirmations { get; set; }

        [JsonProperty(Order = 2)]
        public int height { get; set; }

        [JsonProperty(Order = 5)]
        public uint mediantime { get; set; }

        [JsonProperty(Order = 8)]
        public double difficulty { get; set; }

        [JsonProperty(Order = 9)]
        public string chainwork { get; set; }

        [JsonProperty(Order = 11)]
        public string nextblockhash { get; set; }

        [JsonProperty(Order = 12)]
        public uint time { get; set; }

        #endregion
    }
}
