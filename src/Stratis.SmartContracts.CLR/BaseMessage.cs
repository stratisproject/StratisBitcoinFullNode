using NBitcoin;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public abstract class BaseMessage
    {
        protected BaseMessage(uint160 from, ulong amount, Gas gasLimit)
        {
            this.From = from;
            this.Amount = amount;
            this.GasLimit = gasLimit;
        }

        /// <summary>
        /// The address of the message's sender.
        /// </summary>
        public uint160 From { get; }

        /// <summary>
        /// The value sent with the message.
        /// </summary>
        public ulong Amount { get; }

        /// <summary>
        /// The maximum amount of gas that can be expended while executing the message.
        /// </summary>
        public Gas GasLimit { get; }
    }
}