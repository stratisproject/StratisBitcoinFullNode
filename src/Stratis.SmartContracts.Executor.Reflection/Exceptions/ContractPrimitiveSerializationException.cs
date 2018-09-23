using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    public sealed class ContractPrimitiveSerializationException : SmartContractException
    {
        public ContractPrimitiveSerializationException() { }

        public ContractPrimitiveSerializationException(string message) : base(message) {}
    }
}