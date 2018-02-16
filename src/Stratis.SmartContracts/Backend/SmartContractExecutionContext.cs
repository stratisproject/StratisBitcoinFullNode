namespace Stratis.SmartContracts
{
    /// <summary>
    /// Information about the current state of the blockchain that is passed into the virtual machine.
    /// </summary>
    internal class SmartContractExecutionContext
    {
        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
        internal readonly Block Block;

        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
        internal readonly ulong GasPrice;

        /// <summary>
        /// These are the method parameters to be injected into the method call by the <see cref="SmartContractTransactionExecutor"/>.
        /// </summary>
        internal object[] Parameters { get; private set; }

        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
        internal readonly Message Message;

        internal SmartContractExecutionContext(Block block, Message message, ulong gasPrice, object[] methodParameters = null)
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