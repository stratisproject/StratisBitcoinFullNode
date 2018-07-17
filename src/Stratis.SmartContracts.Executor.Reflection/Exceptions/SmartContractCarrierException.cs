using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    public sealed class SmartContractCarrierException : SmartContractException
    {
        public SmartContractCarrierException() { }

        public SmartContractCarrierException(string message)
            : base(message)
        {
        }
    }
}