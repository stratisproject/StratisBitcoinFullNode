using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Serializer for call data sent with a transaction
    /// </summary>
    public interface ICallDataSerializer
    {
        Result<ContractTxData> Deserialize(byte[] callData);
    }
}