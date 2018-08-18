using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    public class SmartContractBlockHeader : BlockHeader
    {
        /// <summary>
        /// Root of the state trie after execution of this block. 
        /// </summary>
        private uint256 hashStateRoot;
        public uint256 HashStateRoot { get { return this.hashStateRoot; } set { this.hashStateRoot = value; } }

        /// <summary>
        /// Root of the receipt trie after execution of this block.
        /// </summary>
        private uint256 receiptRoot;
        public uint256 ReceiptRoot { get { return this.receiptRoot; } set { this.receiptRoot = value; }  }

        public SmartContractBlockHeader() : base()
        {
            this.hashStateRoot = 0;
            this.receiptRoot = 0;
        }

        /// <summary>
        /// <see cref="ReadWrite(BitcoinStream)"/> overridden so that we can write the <see cref="hashStateRoot"/>.
        /// </summary>
        public override void ReadWrite(BitcoinStream stream)
        {
            base.ReadWrite(stream);
            stream.ReadWrite(ref this.hashStateRoot);
            stream.ReadWrite(ref this.receiptRoot);
        }
    }
}