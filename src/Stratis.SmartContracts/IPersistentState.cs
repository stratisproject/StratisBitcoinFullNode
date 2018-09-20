﻿namespace Stratis.SmartContracts
{
    /// <summary>
    /// Provides functionality for the saving and retrieval of objects inside smart contracts.
    /// </summary>
    public interface IPersistentState
    {
        byte GetByte(string key);

        byte[] GetByteArray(string key);

        char GetChar(string key);

        Address GetAddress(string key);

        bool GetBool(string key);

        int GetInt32(string key);

        uint GetUInt32(string key);

        long GetInt64(string key);

        ulong GetUInt64(string key);

        string GetString(string key);

        sbyte GetSbyte(string key);

        T GetStruct<T>(string key) where T : struct;

        void SetByte(string key, byte value);

        void SetByteArray(string key, byte[] value);

        void SetChar(string key, char value);

        void SetAddress(string key, Address value);

        void SetBool(string key, bool value);

        void SetInt32(string key, int value);

        void SetUInt32(string key, uint value);

        void SetInt64(string key, long value);

        void SetUInt64(string key, ulong value);

        void SetString(string key, string value);

        void SetSByte(string key, sbyte value);

        void SetStruct<T>(string key, T value) where T : struct;
    }
}