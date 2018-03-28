namespace Stratis.SmartContracts
{
    /// <summary>
    /// Holds information about the current block.
    /// </summary>
    public struct Block
    {
        public Block(ulong number, Address coinbase)
        {
            this.Number = number;
            this.Coinbase = coinbase;
        }

        /// <summary>
        /// The coinbase address of the current block. 
        /// The address that will receive the mining award for this block.
        /// </summary>
        public Address Coinbase { get; }

        /// <summary>
        /// The height of the current block.
        /// </summary>
        public ulong Number { get; }
    }
}