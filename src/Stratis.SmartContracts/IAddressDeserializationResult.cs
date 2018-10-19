namespace Stratis.SmartContracts
{
    public interface IAddressDeserializationResult
    {
        /// <summary>
        /// Whether the address was deserialized correctly.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// The address, or default(address) if deserialization was unsuccessful.
        /// </summary>
        Address Address { get; }
    }
}