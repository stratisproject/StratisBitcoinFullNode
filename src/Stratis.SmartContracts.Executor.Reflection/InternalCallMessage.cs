using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class InternalCallMessage : CallMessage
    {
        public InternalCallMessage(uint160 to, uint160 from, ulong amount, Gas gasLimit, MethodCall methodCall)
            : base(to, from, amount, gasLimit, methodCall)
        {
        }
    }
}