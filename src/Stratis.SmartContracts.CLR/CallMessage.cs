using NBitcoin;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Base class for a call message.
    /// </summary>
    public abstract class CallMessage : BaseMessage
    {
        protected CallMessage(uint160 to, uint160 from, ulong amount, Gas gasLimit, MethodCall methodCall)
            : base(from, amount, gasLimit)
        {
            this.To = to;
            this.Method = methodCall;
        }

        /// <summary>
        /// The recipient of the message.
        /// </summary>
        public uint160 To { get; }

        /// <summary>
        /// The method to invoke on the contract and its parameters.
        /// </summary>
        public MethodCall Method { get; }
    }
}