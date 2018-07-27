using System;

namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    public class StratisCompilationException : Exception
    {
        public StratisCompilationException() { }
        public StratisCompilationException(string message) : base(message) { }
    }
}