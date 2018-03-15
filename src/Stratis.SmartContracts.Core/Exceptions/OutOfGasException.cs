namespace Stratis.SmartContracts.Core.Exceptions
{
    public class OutOfGasException : SmartContractException
    {
        public OutOfGasException() { }

        public OutOfGasException(string message) : base(message) { }
    }
}