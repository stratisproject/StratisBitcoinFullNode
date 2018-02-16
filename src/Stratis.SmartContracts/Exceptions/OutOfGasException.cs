using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Exceptions
{
    public class OutOfGasException : SmartContractException
    {
        public OutOfGasException() { }

        public OutOfGasException(string message) : base(message) {}
    }
}
