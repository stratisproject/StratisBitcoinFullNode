using System;

namespace Stratis.SmartContracts.Core.Exceptions
{
    public class ConstructorInvocationException : Exception
    {
        public ConstructorInvocationException() { }
        public ConstructorInvocationException(string message) : base(message) { }
    }
}