using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    public class OutOfGasException : SmartContractException
    {
        public OutOfGasException(string message) : base(message) { }
    }
}