namespace Stratis.SmartContracts
{
    /// <summary>
    /// Provides functionality for the saving and retrieval of objects inside smart contracts.
    /// </summary>
    public interface IPersistentState
    {
        byte[] GetBytes(string key);

        void SetBytes(string key, byte[] bytes);

        char GetAsChar(string key);

        Address GetAsAddress(string key);

        bool GetAsBool(string key);

        int GetAsInt32(string key);

        uint GetAsUInt32(string key);

        long GetAsInt64(string key);

        ulong GetAsUInt64(string key);

        string GetAsString(string key);

        T GetAsStruct<T>(string key) where T : struct;

        void SetChar(string key, char value);

        void SetAddress(string key, Address value);

        void SetBool(string key, bool value);

        void SetInt32(string key, int value);

        void SetUInt32(string key, uint value);

        void SetInt64(string key, long value);

        void SetUInt64(string key, ulong value);

        void SetString(string key, string value);

        void SetStruct<T>(string key, T value) where T : struct;
    }
}