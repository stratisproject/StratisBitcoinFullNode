using System;

namespace Stratis.SmartContracts
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

        /// <summary>
        /// Initialise a mapping in the Key/Value store that uses the given key as its prefix.
        /// </summary>        
        ISmartContractMapping<byte> GetByteMapping(string name);

        ISmartContractMapping<byte[]> GetByteArrayMapping(string name);

        ISmartContractMapping<char> GetCharMapping(string name);

        ISmartContractMapping<Address> GetAddressMapping(string name);

        ISmartContractMapping<bool> GetBoolMapping(string name);

        ISmartContractMapping<int> GetInt32Mapping(string name);

        ISmartContractMapping<uint> GetUInt32Mapping(string name);

        ISmartContractMapping<long> GetInt64Mapping(string name);

        ISmartContractMapping<ulong> GetUInt64Mapping(string name);

        ISmartContractMapping<string> GetStringMapping(string name);

        ISmartContractMapping<sbyte> GetSByteMapping(string name);

        ISmartContractMapping<T> GetStructMapping<T>(string name) where T : struct;

        /// <summary>
        /// Initialise a list in the Key/Value store that uses the given name as its prefix.
        /// </summary>
        ISmartContractList<byte> GetByteList(string name);

        ISmartContractList<byte[]> GetByteArrayList(string name);

        ISmartContractList<char> GetCharList(string name);

        ISmartContractList<Address> GetAddressList(string name);

        ISmartContractList<bool> GetBoolList(string name);

        ISmartContractList<int> GetInt32List(string name);

        ISmartContractList<uint> GetUInt32List(string name);

        ISmartContractList<long> GetInt64List(string name);

        ISmartContractList<ulong> GetUInt64List(string name);

        ISmartContractList<string> GetStringList(string name);

        ISmartContractList<sbyte> GetSByteList(string name);

        ISmartContractList<T> GetStructList<T>(string name) where T : struct;
    }
}