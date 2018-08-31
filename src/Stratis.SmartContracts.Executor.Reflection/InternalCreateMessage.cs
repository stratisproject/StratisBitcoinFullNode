using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class InternalCreateMessage : BaseMessage
    {
        public InternalCreateMessage(uint160 from, ulong amount, Gas gasLimit, MethodCall methodCall, string typeName)
            : base(from, amount, gasLimit)
        {
            this.Method = methodCall;
            this.Type = typeName;
        }

        /// <summary>
        /// Internal creates need a method call with params and an empty method name.
        /// </summary>
        public MethodCall Method { get; }

        /// <summary>
        /// Internal creates need to specify the Type they are creating.
        /// </summary>
        public string Type { get; }
    }
}