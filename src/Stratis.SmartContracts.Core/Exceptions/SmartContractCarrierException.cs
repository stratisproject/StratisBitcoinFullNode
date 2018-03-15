namespace Stratis.SmartContracts.Core.Exceptions
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