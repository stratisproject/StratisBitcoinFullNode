using System;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Provides functionality for the saving and retrieval of objects inside smart contracts.
    /// </summary>
    public interface IPersistentState
    {
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
        /// deserialize the value to an Address. If deserialization is unsuccessful, returns default(Address).
        /// </summary>
        Address GetAddress(string key);

        /// <summary>
        /// Gets the bytes set at the value pointed to by the given key, and uses <see cref="ISerializer.ToBool"/> to
        /// deserialize the value to a bool. If deserialization is unsuccessful, returns default(bool).
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

        void SetBytes(string key, byte[] value);

        void SetChar(string key, char value);

        void SetAddress(string key, Address value);

        void SetBool(string key, bool value);

        void SetInt32(string key, int value);

        void SetUInt32(string key, uint value);

        void SetInt64(string key, long value);

        void SetUInt64(string key, ulong value);

        void SetString(string key, string value);

        void SetStruct<T>(string key, T value) where T : struct;

        void SetArray(string key, Array a);
    }
}