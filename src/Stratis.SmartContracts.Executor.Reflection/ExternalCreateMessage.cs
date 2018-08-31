using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class ExternalCreateMessage : BaseMessage
    {
        public ExternalCreateMessage(uint160 from, ulong amount, Gas gasLimit, byte[] code, MethodCall methodCall)
            : base(from, amount, gasLimit)
        {
            this.Code = code;
            this.Method = methodCall;
        }

        public byte[] Code { get; }

        public MethodCall Method { get; }
    }
}