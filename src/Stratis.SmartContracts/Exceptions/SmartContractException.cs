using System;

namespace Stratis.SmartContracts.Exceptions
{
    public abstract class SmartContractException : Exception
    {
        protected SmartContractException() { }

        protected SmartContractException(string message)
            : base(message)
        {

        }
    }
}