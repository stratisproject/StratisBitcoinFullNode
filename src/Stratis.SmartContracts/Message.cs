namespace Stratis.SmartContracts
{
    public class Message
    {
        public Address ContractAddress { get; }

        public Address Sender { get; }

        public Gas GasLimit { get;  }

        public ulong Value { get; }

        public Message(Address contractAddress, Address sender, ulong value, Gas gasLimit)
        {
            ContractAddress = contractAddress;
            Sender = sender;
            Value = value;
            GasLimit = gasLimit;
        }
    }
}
