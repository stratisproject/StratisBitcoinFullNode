using System;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Provides functionality for the saving and retrieval of objects inside smart contracts.
    /// </summary>
    public interface IPersistentState
    {
        byte[] GetBytes(string key);

        char GetChar(string key);

        Address GetAddress(string key);

        bool GetBool(string key);

        int GetInt32(string key);

        uint GetUInt32(string key);

        long GetInt64(string key);

        ulong GetUInt64(string key);

        string GetString(string key);

        T GetStruct<T>(string key) where T : struct;

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