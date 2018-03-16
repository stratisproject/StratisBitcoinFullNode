namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Information about the current state of the blockchain that is passed into the virtual machine.
    /// </summary>
    public sealed class SmartContractExecutionContext : ISmartContractExecutionContext
    {
        /// <inheritdoc/>
        public Block Block { get; }

        /// <inheritdoc/>
        public ulong GasPrice { get; }

        /// <inheritdoc/>
        public object[] Parameters { get; private set; }

        /// <inheritdoc/>
        public Message Message { get; }

        public SmartContractExecutionContext(Block block, Message message, ulong gasPrice, object[] methodParameters = null)
        {
            //TODO: Add some null checks here

            this.Block = block;
            this.Message = message;
            this.GasPrice = gasPrice;

            if (methodParameters != null && methodParameters.Length > 0)
                this.Parameters = methodParameters;
        }
    }
}