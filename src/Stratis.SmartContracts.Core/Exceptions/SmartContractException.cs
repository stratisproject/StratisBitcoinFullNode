using System;

namespace Stratis.SmartContracts.Core.Exceptions
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