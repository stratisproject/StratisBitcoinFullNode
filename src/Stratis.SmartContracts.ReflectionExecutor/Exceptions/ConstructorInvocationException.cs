using System;

namespace Stratis.SmartContracts.ReflectionExecutor.Exceptions
{
    public class ConstructorInvocationException : Exception
    {
        public ConstructorInvocationException() { }
        public ConstructorInvocationException(string message) : base(message) { }
    }
}