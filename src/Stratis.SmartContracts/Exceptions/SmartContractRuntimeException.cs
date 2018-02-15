using System;

namespace Stratis.SmartContracts.Exceptions
{
    public class SmartContractRuntimeException : Exception
    {
        public SmartContractRuntimeException() { }

        public SmartContractRuntimeException(string message) : base(message)
        {

        }
    }
}