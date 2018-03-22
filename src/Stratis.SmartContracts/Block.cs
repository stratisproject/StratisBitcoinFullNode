namespace Stratis.SmartContracts
{
    public struct Block
    {
        public Block(ulong number, Address coinbase)
        {
            this.Number = number;
            this.Coinbase = coinbase;
        }

        public Address Coinbase { get; }
        public ulong Number { get; }
    }
}