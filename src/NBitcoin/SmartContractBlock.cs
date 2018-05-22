namespace NBitcoin
{
    public class SmartContractBlockHeader : BlockHeader
    {
        /// <summary>
        /// Root of the state trie after execution of this block. 
        /// </summary>
        private uint256 hashStateRoot;
        public uint256 HashStateRoot { get { return this.hashStateRoot; } set { this.hashStateRoot = value; } }

        /// <summary>
        /// <see cref="ReadWrite(BitcoinStream)"/> overridden so that we can write the <see cref="hashStateRoot"/>.
        /// </summary>
        public override void ReadWrite(BitcoinStream stream)
        {
            base.ReadWrite(stream);
            stream.ReadWrite(ref this.hashStateRoot);
        }

        /// <summary>
        /// <see cref="SetNull()"/> overridden so that we can set the <see cref="hashStateRoot"/> to 0.
        /// </summary>
        internal override void SetNull()
        {
            base.SetNull();
            this.hashStateRoot = 0;
        }
    }
}