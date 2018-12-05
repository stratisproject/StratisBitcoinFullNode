using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.CLR.Exceptions
{
    public class OutOfGasException : SmartContractException
    {
        public OutOfGasException(string message) : base(message) { }
    }
}