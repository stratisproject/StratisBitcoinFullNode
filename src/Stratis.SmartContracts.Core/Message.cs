namespace Stratis.SmartContracts.Core
{
    public sealed class Message : IMessage
    {
        private readonly IGasMeter gasMeter;


        /// <inheritdoc/>
        public Address ContractAddress { get; }

        /// <inheritdoc/>
        public Address Sender { get; }

        /// <inheritdoc/>
        public Gas GasAvailable
        {
            get
            {
                return this.gasMeter.GasAvailable;
            }
        }

        /// <inheritdoc/>
        public Gas GasLimit { get; }

        /// <inheritdoc/>
        public ulong Value { get; }

        public Message(Address contractAddress, Address sender, ulong value, Gas gasLimit, IGasMeter gasMeter)
        {
            this.ContractAddress = contractAddress;
            this.Sender = sender;
            this.Value = value;
            this.GasLimit = gasLimit;
            this.gasMeter = gasMeter;
        }
    }
}