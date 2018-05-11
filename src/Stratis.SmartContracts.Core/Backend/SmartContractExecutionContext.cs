using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core.Backend
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

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Format("{0}:{1},{2}:{3}", nameof(this.Block.Coinbase), this.Block.Coinbase, nameof(this.Block.Number), this.Block.Number));
            builder.AppendLine(string.Format("{0}:{1}", nameof(this.ContractAddress), this.ContractAddress));
            builder.AppendLine(string.Format("{0}:{1}", nameof(this.GasPrice), this.GasPrice));
            builder.AppendLine(string.Format("{0}:{1},{2}:{3},{4}:{5},{6}:{7}", nameof(this.Message.ContractAddress), this.Message.ContractAddress, nameof(this.Message.GasLimit), this.Message.GasLimit, nameof(this.Message.Sender), this.Message.Sender, nameof(this.Message.Value), this.Message.Value));
            builder.AppendLine(string.Format("{0}:{1}", nameof(this.Parameters), this.Parameters));
            return builder.ToString();
        }
    }
}