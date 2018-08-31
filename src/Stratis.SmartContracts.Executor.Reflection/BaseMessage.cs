using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
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
        /// All transfers have a recipient.
        /// </summary>
        public uint160 From { get; }

        /// <summary>
        /// All transfers have an amount.
        /// </summary>
        public ulong Amount { get; }

        /// <summary>
        /// All transfers have some gas limit associated with them. This is even required for fallback calls.
        /// </summary>
        public Gas GasLimit { get; }
    }
}