using System;

namespace Stratis.SmartContracts.ReflectionExecutor.Exceptions
{
    public class StratisCompilationException : Exception
    {
        public StratisCompilationException() { }
        public StratisCompilationException(string message) : base(message) { }
    }
}