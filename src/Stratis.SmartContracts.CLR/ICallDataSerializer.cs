using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Serializer for call data sent with a transaction
    /// </summary>
    public interface ICallDataSerializer
    {
        /// <summary>
        /// Deserializes a <see cref="ContractTxData"/> object from raw bytes of a Transaction's Script.
        /// </summary>
        Result<ContractTxData> Deserialize(byte[] callData);

        /// <summary>
        /// Serializes a <see cref="ContractTxData"/> object to raw bytes.
        /// </summary>
        byte[] Serialize(ContractTxData contractTxData);
    }
}