namespace Stratis.SmartContracts.Exceptions
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