using System;

namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    public class ConstructorInvocationException : Exception
    {
        public ConstructorInvocationException() { }
        public ConstructorInvocationException(string message) : base(message) { }
    }
}