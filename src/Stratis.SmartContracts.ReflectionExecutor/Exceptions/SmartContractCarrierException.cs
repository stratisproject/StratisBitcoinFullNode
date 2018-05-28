using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.ReflectionExecutor.Exceptions
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