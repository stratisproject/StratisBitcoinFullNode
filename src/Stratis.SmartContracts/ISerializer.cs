namespace Stratis.SmartContracts
{
    public interface ISerializer
    {
        // TODO: Document the requirements for inputs for these fields?

        /// <summary>
        /// Serializes an address into its 20-bytes representation.
        /// </summary>
        byte[] Serialize(Address address);

        /// <summary>
        /// Serializes a boolean into a byte via BitConverter.
        /// </summary>
        byte[] Serialize(bool b);

        /// <summary>
        /// Serializes an integer into bytes via BitConverter. 
        /// </summary>
        byte[] Serialize(int i);

        /// <summary>
        /// Serializes a long into bytes via BitConverter.
        /// </summary>
        byte[] Serialize(long l);

        /// <summary>
        /// Serializes an unsigned integer into bytes via BitConverter.
        /// </summary>
        byte[] Serialize(uint u);

        /// <summary>
        /// Serializes a unsigned long into bytes via BitConverter.
        /// </summary>
        byte[] Serialize(ulong ul);

        /// <summary>
        /// Serializes a string into its UTF8 encoding.
        /// </summary>
        byte[] Serialize(string s);

        /// <summary>
        /// Serializes bytes into a boolean via BitConverter.
        /// </summary>
        bool ToBool(byte[] val);

        /// <summary>
        /// Serializes 20-bytes into an address.
        /// </summary>
        Address ToAddress(byte[] val);

        /// <summary>
        /// Serializes first 4 bytes of a byte array into an integer.
        /// </summary>
        int ToInt32(byte[] val);

        /// <summary>
        /// Serializes first 4 bytes of a byte array into an unsigned integer.
        /// </summary>
        uint ToUInt32(byte[] val);

        /// <summary>
        /// Serializes first 8 bytes of a  byte array into a long. 
        /// </summary>
        long ToInt64(byte[] val);

        /// <summary>
        /// Serializes first 8 bytes of a byte array into an unsigned long.
        /// </summary>
        ulong ToUInt64(byte[] val);

        /// <summary>
        /// Serializes UTF8-encoded bytes into a string.
        /// </summary>
        string ToString(byte[] val);
    }
}
