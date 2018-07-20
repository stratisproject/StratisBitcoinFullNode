using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class CallData : ICallData
    {
        public CallData(Gas gasLimit, uint160 address, string method, object[] methodParameters = null)
        {
            this.GasLimit = gasLimit;
            this.ContractAddress = address;
            this.MethodName = method;
            this.MethodParameters = methodParameters;
        }

        public object[] MethodParameters { get; }
        public uint160 ContractAddress { get; }
        public Gas GasLimit { get; }
        public string MethodName { get; }
    }
}