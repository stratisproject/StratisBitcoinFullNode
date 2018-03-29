namespace Stratis.SmartContracts
{
    /// <summary>
    /// Provides functionality for the saving and retrieval of objects inside smart contracts.
    /// </summary>
    public interface IPersistentState
    {
        /// <summary>
        /// Retrieve the bytes at the given key and cast it to type T.
        /// </summary>
        T GetObject<T>(string key);

        /// <summary>
        /// Serialize the given object and save it at the given key.
        /// </summary>
        void SetObject<T>(string key, T obj);

        /// <summary>
        /// Initialise a mapping in the Key/Value store that uses the given key as its prefix.
        /// </summary>
        ISmartContractMapping<V> GetMapping<V>(string name);

        /// <summary>
        /// Initialise a list in the Key/Value store that uses the given name as its prefix.
        /// </summary>
        ISmartContractList<T> GetList<T>(string name);
    }
}