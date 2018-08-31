using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class ContractTransferMessage : InternalCallMessage
    {
        public ContractTransferMessage(uint160 to, uint160 from, ulong amount, Gas gasLimit) 
            : base(to, from, amount, gasLimit, MethodCall.Receive())
        {
        }
    }
}