using System;

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
        /// Serializes an array into its RLP-encoded format. If the given array is null, returns null.
        /// </summary>
        /// <param name="a">The array to serialize. The DeclaringType of the array must be one of the supported contract primitives.</param>
        byte[] Serialize(Array a);

        /// <summary>
        /// Serializes a struct into its RLP-encoded format.
        /// </summary>
        /// <param name="s">The struct to serialize.</param>
        byte[] Serialize<T>(T s) where T : struct;

        /// <summary>
        /// Deserializes bytes into a boolean via BitConverter. If the given bytes are null, empty, or deserialization fails, returns default(bool).
        /// </summary>
        bool ToBool(byte[] val);

        /// <summary>
        /// Deserializes 20-bytes into an address. If the given bytes are null, empty, or deserialization fails, returns default(Address).
        /// </summary>
        Address ToAddress(byte[] val);

        /// <summary>
        /// Deserializes first 4 bytes of a byte array into an integer. If the given bytes are null, empty, or deserialization fails, returns default(int).
        /// </summary>
        int ToInt32(byte[] val);

        /// <summary>
        /// Deserializes first 4 bytes of a byte array into an unsigned integer.If the given bytes are null, empty, or deserialization fails, returns default(uint).
        /// </summary>
        uint ToUInt32(byte[] val);

        /// <summary>
        /// Deserializes first 8 bytes of a  byte array into a long. If the given bytes are null, empty, or deserialization fails, returns default(long).
        /// </summary>
        long ToInt64(byte[] val);

        /// <summary>
        /// Deserializes first 8 bytes of a byte array into an unsigned long. If the given bytes are null, empty, or deserialization fails, returns default(ulong).
        /// </summary>
        ulong ToUInt64(byte[] val);

        /// <summary>
        /// Deserializes UTF8-encoded bytes into a string. If the given bytes are null, empty, or deserialization fails, returns <see cref="string.Empty"/>.
        /// </summary>
        string ToString(byte[] val);

        /// <summary>
        /// Deserializes RLP-encoded bytes to an array. If the given bytes are null, empty, or deserialization fails, returns new T[0].
        /// </summary>
        /// <typeparam name="T">DeclaringType in the array to return. Must be one of the supported contract primitives.</typeparam>
        T[] ToArray<T>(byte[] val);

        /// <summary>
        /// Deserializes RLP-encoded bytes to an struct. If the given bytes are null, empty, or deserialization fails, returns default(T).
        /// </summary>
        /// <typeparam name="T">The Type of struct to return</typeparam>
        T ToStruct<T>(byte[] val) where T: struct;
    }
}
