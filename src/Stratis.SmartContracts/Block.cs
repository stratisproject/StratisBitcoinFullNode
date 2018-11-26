namespace Stratis.SmartContracts
{
    public struct Block : IBlock
    {
        /// <inheritdoc/>
        public Address Coinbase { get; }

        /// <inheritdoc/>
        public ulong Number { get; }

        public Block(ulong number, Address coinbase)
        {
            this.Number = number;
            this.Coinbase = coinbase;
        }
    }
}