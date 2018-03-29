namespace Stratis.SmartContracts.Core
{
    public struct Block : IBlock
    {
        /// <summary>
        /// The coinbase address of the current block. 
        /// The address that will receive the mining award for this block.
        /// </summary>
        public Address Coinbase { get; }

        /// <summary>
        /// The height of the current block.
        /// </summary>
        public ulong Number { get; }

        public Block(ulong number, Address coinbase)
        {
            this.Number = number;
            this.Coinbase = coinbase;
        }
    }
}