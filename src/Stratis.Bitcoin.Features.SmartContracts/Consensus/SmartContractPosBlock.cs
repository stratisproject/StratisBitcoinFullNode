using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    /// <summary>
    /// A smart contract proof of stake block that contains the additional block signature serialization.
    /// </summary>
    public class SmartContractPosBlock : PosBlock
    {
        ///// <summary>A block signature - signed by one of the coin base txout[N]'s owner.</summary>
        //private BlockSignature blockSignature = new BlockSignature();

        internal SmartContractPosBlock(SmartContractPosBlockHeader blockHeader) : base(blockHeader)
        {
        }

        ///// <summary>
        ///// The block signature type.
        ///// </summary>
        //public BlockSignature BlockSignature
        //{
        //    get { return this.blockSignature; }
        //    set { this.blockSignature = value; }
        //}

        ///// <summary>
        ///// The additional serialization of the block POS block.
        ///// </summary>
        //public override void ReadWrite(BitcoinStream stream)
        //{
        //    base.ReadWrite(stream);
        //    stream.ReadWrite(ref this.blockSignature);

        //    this.BlockSize = stream.Serializing ? stream.Counter.WrittenBytes : stream.Counter.ReadBytes;
        //}
    }
}