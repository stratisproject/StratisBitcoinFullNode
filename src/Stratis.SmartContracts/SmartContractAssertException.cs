using System;

namespace Stratis.SmartContracts
{
    public class SmartContractAssertException : Exception
    {
        public SmartContractAssertException(string message) : base(message) { }
    }
}
