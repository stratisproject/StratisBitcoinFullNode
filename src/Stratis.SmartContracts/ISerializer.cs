using System;

namespace Stratis.SmartContracts
{
    public interface ISerializer
    {
        /// <summary>
        /// Serializes a char into its 2-byte representation.
        /// </summary>
        byte[] Serialize(char c);

        /// <summary>
        /// Serializes an address into its 20-byte representation.
        /// </summary>
        byte[] Serialize(Address address);

        /// <summary>
        /// Serializes a boolean into a byte via BitConverter.
        /// </summary>
        byte[] Serialize(bool b);

        /// <summary>
        /// Serializes an integer into its 4-byte representation. 
        /// </summary>
        byte[] Serialize(int i);

        /// <summary>
        /// Serializes a long into its 8-byte representation.
        /// </summary>
        byte[] Serialize(long l);

        /// <summary>
        /// Serializes an unsigned integer into its 4-byte representation.
        /// </summary>
        byte[] Serialize(uint u);

        /// <summary>
        /// Serializes a unsigned long into its 8-byte representation.
        /// </summary>
        byte[] Serialize(ulong ul);

        /// <summary>
        /// Serializes a string into its UTF8 encoded byte array.
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
        /// Deserializes 20-bytes into an address. If the given bytes are null, empty, or deserialization fails, returns <see cref="Address.Zero"/>.
        /// </summary>
        Address ToAddress(byte[] val);

        /// <summary>
        /// Deserializes a string into an address. If the given string is null, empty, or deserialization fails, returns <see cref="Address.Zero"/>.
        /// </summary>
        Address ToAddress(string val);

        /// <summary>
        /// Deserializes the first 4 bytes of a byte array into an integer. If the given bytes are null, empty, or deserialization fails, returns default(int).
        /// </summary>
        int ToInt32(byte[] val);

        /// <summary>
        /// Deserializes the first 4 bytes of a byte array into an unsigned integer.If the given bytes are null, empty, or deserialization fails, returns default(uint).
        /// </summary>
        uint ToUInt32(byte[] val);

        /// <summary>
        /// Deserializes the first 8 bytes of a byte array into a long. If the given bytes are null, empty, or deserialization fails, returns default(long).
        /// </summary>
        long ToInt64(byte[] val);

        /// <summary>
        /// Deserializes the first 8 bytes of a byte array into an unsigned long. If the given bytes are null, empty, or deserialization fails, returns default(ulong).
        /// </summary>
        ulong ToUInt64(byte[] val);

        /// <summary>
        /// Deserializes bytes into a string using UTF8. If the given bytes are null, empty, or deserialization fails, returns <see cref="string.Empty"/>.
        /// </summary>
        string ToString(byte[] val);

        /// <summary>
        /// Deserializes 2 bytes into a Unicode character.
        /// </summary>
        char ToChar(byte[] val);

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
