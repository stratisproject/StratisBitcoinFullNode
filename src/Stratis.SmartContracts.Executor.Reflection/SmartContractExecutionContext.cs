using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Information about the current state of the blockchain that is passed into the virtual machine.
    /// </summary>
    public sealed class SmartContractExecutionContext : ISmartContractExecutionContext
    {
        /// <inheritdoc/>
        public IBlock Block { get; }

        /// <inheritdoc/>
        public uint160 ContractAddress { get; set; }

        /// <inheritdoc/>
        public ulong GasPrice { get; }

        /// <inheritdoc/>
        public IMessage Message { get; }

        /// <inheritdoc/>
        public object[] Parameters { get; private set; }

        public SmartContractExecutionContext(IBlock block, IMessage message, uint160 contractAdddress, ulong gasPrice, object[] methodParameters = null)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(message, nameof(message));

            this.Block = block;
            this.Message = message;
            this.GasPrice = gasPrice;
            this.ContractAddress = contractAdddress;

            if (methodParameters != null && methodParameters.Length > 0)
                this.Parameters = methodParameters;
        }
    }
}