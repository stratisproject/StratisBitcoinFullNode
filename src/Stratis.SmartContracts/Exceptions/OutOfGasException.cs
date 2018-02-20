namespace Stratis.SmartContracts.Exceptions
{
    public class OutOfGasException : SmartContractException
    {
        public OutOfGasException() { }

        public OutOfGasException(string message) : base(message) { }
    }
}