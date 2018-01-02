using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Exceptions
{
    public class NonDeterministicCallException : StratisCompilationException
    {
        public NonDeterministicCallException() { }
        public NonDeterministicCallException(string message): base(message) { }
    }
}
