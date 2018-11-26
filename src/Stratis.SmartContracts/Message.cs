namespace Stratis.SmartContracts
{
    public sealed class Message : IMessage
    {
        /// <inheritdoc/>
        public Address ContractAddress { get; }

        /// <inheritdoc/>
        public Address Sender { get; }

        /// <inheritdoc/>
        public ulong Value { get; }

        public Message(Address contractAddress, Address sender, ulong value)
        {
            this.ContractAddress = contractAddress;
            this.Sender = sender;
            this.Value = value;
        }
    }
}