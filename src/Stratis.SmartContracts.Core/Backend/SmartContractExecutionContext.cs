namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Information about the current state of the blockchain that is passed into the virtual machine.
    /// </summary>
    public class SmartContractExecutionContext : ISmartContractExecutionContext
    {
        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
        public Block Block { get; }

        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
        public ulong GasPrice { get; }

        /// <summary>
        /// These are the method parameters to be injected into the method call by the <see cref="Stratis.SmartContracts.Core.SmartContractTransactionExecutor"/>.
        /// </summary>
        public object[] Parameters { get; private set; }

        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
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