using System;

namespace Stratis.SmartContracts.Core.Exceptions
{
    public class StratisCompilationException : Exception
    {
        public StratisCompilationException() { }
        public StratisCompilationException(string message): base(message) { }
    }
}
