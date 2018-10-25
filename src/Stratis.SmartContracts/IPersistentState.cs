using System;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Provides functionality for the saving and retrieval of objects inside smart contracts.
    /// </summary>
    public interface IPersistentState
    {
        /// <summary>
        /// Returns true if a contract exists at the given Address. If the supplied Address is invalid, returns false.
        /// </summary>
        bool IsContract(Address address);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given byte array.
        /// </summary>
        byte[] GetBytes(byte[] key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key.
        /// </summary>
        byte[] GetBytes(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToChar"/> to
        /// deserialize the value to a char. If deserialization is unsuccessful, returns default(char).
        /// </summary>
        char GetChar(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToAddress"/> to
        /// deserialize the value to an Address. If deserialization is unsuccessful, returns returns <see cref="Address.Zero"/>.
        /// </summary>
        Address GetAddress(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToBool"/> to
        /// deserialize the value to a bool. If deserialization is unsuccessful, returns returns <see cref="Address.Zero"/>.
        /// </summary>
        bool GetBool(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToInt32"/> to
        /// deserialize the value to an int. If deserialization is unsuccessful, returns default(int).
        /// </summary>
        int GetInt32(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToUInt32"/> to
        /// deserialize the value to a uint. If deserialization is unsuccessful, returns default(uint).
        /// </summary>
        uint GetUInt32(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToInt64"/> to
        /// deserialize the value to a long. If deserialization is unsuccessful, returns default(long).
        /// </summary>
        long GetInt64(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToUInt64"/> to
        /// deserialize the value to a ulong. If deserialization is unsuccessful, returns default(ulong).
        /// </summary>
        ulong GetUInt64(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToString(byte[])"/> to
        /// deserialize the value to a string. If deserialization is unsuccessful, returns <see cref="string.Empty"/>.
        /// </summary>
        string GetString(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToStruct{T}(byte[])"/> to
        /// deserialize the value to a struct. If deserialization is unsuccessful, returns default(T).
        /// </summary>
        T GetStruct<T>(string key) where T : struct;

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToArray{T}(byte[])"/> to
        /// deserialize the value to an Array. If deserialization is unsuccessful, returns new T[0].
        /// </summary>
        T[] GetArray<T>(string key);

        /// <summary>
        /// Sets the given bytes against the given key in state storage.
        /// </summary>
        void SetBytes(byte[] key, byte[] value);

        /// <summary>
        /// Sets the given bytes against the given key in state storage.
        /// </summary>
        void SetBytes(string key, byte[] value);

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize(char)"/> to serialize a char to its 2-byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetChar(string key, char value);

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize(Address)"/> to serialize an Address to its 20-byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetAddress(string key, Address value);

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize(bool)"/> to serialize a bool to its 1-byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetBool(string key, bool value);

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize(int)"/> to serialize an int to its 4-byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetInt32(string key, int value);

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize(uint)"/> to serialize a uint to its 4-byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetUInt32(string key, uint value);

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize(long)"/> to serialize a long to its 8-byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetInt64(string key, long value);

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize(ulong)"/> to serialize a ulong to its 8-byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetUInt64(string key, ulong value);

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize(string)"/> to serialize a string to its UTF8 encoded byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetString(string key, string value);

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize{T}"/> to serialize a struct to its byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetStruct<T>(string key, T value) where T : struct;

        /// <summary>
        /// Uses <see cref="ISerializer.Serialize(Array)"/> to serialize an array of primitives to its byte representation.
        /// Sets the serialized bytes against the given key in state storage.
        /// </summary>
        void SetArray(string key, Array a);

        /// <summary>
        /// Removes any bytes set at the value pointed to by the given key.
        /// </summary>
        void Clear(string key);
    }
}