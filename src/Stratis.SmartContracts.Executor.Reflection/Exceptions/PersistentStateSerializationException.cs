using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    public sealed class PersistentStateSerializationException : SmartContractException
    {
        public PersistentStateSerializationException() { }

        public PersistentStateSerializationException(string message) : base(message) {}
    }
}