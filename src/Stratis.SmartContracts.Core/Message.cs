namespace Stratis.SmartContracts.Core
{
    public sealed class Message : IMessage
    {
        public Address ContractAddress { get; }

        public Address Sender { get; }

        public Gas GasLimit { get; }

        public ulong Value { get; }

        public Message(Address contractAddress, Address sender, ulong value, Gas gasLimit)
        {
            this.ContractAddress = contractAddress;
            this.Sender = sender;
            this.Value = value;
            this.GasLimit = gasLimit;
        }
    }
}