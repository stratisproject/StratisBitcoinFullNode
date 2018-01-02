using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Exceptions
{
    public class OutOfGasException : SmartContractRuntimeException
    {
        public OutOfGasException() { }

        public OutOfGasException(string message) : base(message) {}
    }
}
