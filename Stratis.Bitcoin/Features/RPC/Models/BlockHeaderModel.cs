using NBitcoin;
using NBitcoin.DataEncoders;

namespace Stratis.Bitcoin.Features.RPC.Models
{
#pragma warning disable IDE1006 // Naming Styles (ignore lowercase)

    /// <summary>
    /// Data structure for RPC block headers
    /// 
    /// <see cref="https://bitcoin.org/en/developer-reference#getblockheader"/>
    /// </summary>
    public class BlockHeaderModel
    {
        /// <summary>
        /// Constructs a RPC BlockHeaderModel from a block header object
        /// </summary>
        /// <param name="blockHeader">the block header</param>
        public BlockHeaderModel(BlockHeader blockHeader)
        {           
            if (blockHeader != null)
            {
                this.version = (uint)blockHeader.Version;
                this.previousblockhash = blockHeader.HashPrevBlock.ToString();
                this.merkleroot = blockHeader.HashMerkleRoot.ToString();
                this.time = blockHeader.Time;
                byte[] bytes = this.GetBytes(blockHeader.Bits.ToCompact());
                string encodedBytes = Encoders.Hex.EncodeData(bytes);
                this.bits = encodedBytes;
                this.nonce = (int)blockHeader.Nonce;
            }
        }

        /// <summary>
        /// The blocks version number
        /// </summary>
        public uint version { get; set; }

        /// <summary>
        /// The merkle root for this block encoded as hex in RPC byte order
        /// </summary>
        public string merkleroot { get; set; }

        /// <summary>
        /// The nonce thich was successful at turning this particular block
        /// into one that could be added to the best block chain
        /// </summary>
        public int nonce { get; set; }

        /// <summary>
        /// The target threshhold this blocks header had to pass
        /// </summary>
        public string bits { get; set; }

        /// <summary>
        /// The hash of the header of the previous block,
        /// encoded as hex in RPC byte order
        /// </summary>
        public string previousblockhash { get; set; }

        /// <summary>
        /// The block time in seconds since epoch (Jan 1 1970 GMT)
        /// </summary>
        public uint time { get; set; }

        /// <summary>
        /// Convert compact of miner challenge to byte format
        /// serialized for transmission via RPC
        /// <seealso cref="Target"/>
        /// </summary>
        /// <param name="compact">compact representation of challenge</param>
        /// <returns>byte representation of challenge</returns>
        private byte[] GetBytes(uint compact)
        {
            return new byte[]
            {
                (byte)(compact >> 24),
                (byte)(compact >> 16),
                (byte)(compact >> 8),
                (byte)(compact)
            };
        }
    }
}
