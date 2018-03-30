namespace Stratis.SmartContracts.Core
{
    public sealed class Message : IMessage
    {
        /// <inheritdoc/>
        /// <summary>
        /// The address of the contract currently being executed.
        /// </summary>
        public Address ContractAddress { get; }

        /// <inheritdoc/>
        /// <summary>
        /// The address that called this contract.
        /// </summary>
        public Address Sender { get; }

        /// <inheritdoc/>
        /// <summary>
        /// The total gas allocated allowed to be spend during contract execution.
        /// </summary>
        public Gas GasLimit { get; }

        /// <inheritdoc/>
        /// <summary>
        /// The amount of STRAT sent in this call. 
        /// </summary>
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