using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractBlockHeader : BlockHeader
    {
        /// <summary>
        /// Root of the state trie after execution of this block. 
        /// </summary>
        private uint256 hashStateRoot;
        public uint256 HashStateRoot { get { return this.hashStateRoot; } set { this.hashStateRoot = value; } }

        public SmartContractBlockHeader() : base()
        {
            this.hashStateRoot = 0;

        }

        /// <summary>
        /// <see cref="ReadWrite(BitcoinStream)"/> overridden so that we can write the <see cref="hashStateRoot"/>.
        /// </summary>
        public override void ReadWrite(BitcoinStream stream)
        {
            base.ReadWrite(stream);
            stream.ReadWrite(ref this.hashStateRoot);
        }
    }
}