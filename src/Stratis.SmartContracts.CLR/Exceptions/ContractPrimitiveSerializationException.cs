using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.CLR.Exceptions
{
    public sealed class ContractPrimitiveSerializationException : SmartContractException
    {
        public ContractPrimitiveSerializationException(string message) : base(message) {}
    }
}