namespace Stratis.SmartContracts
{
    public interface ISmartContractMapping<V>
    {
        /// <summary>
        /// Store an item in the mapping at the given key. In PersistentState, the given item will be stored at {name}[{key}].
        /// </summary>
        void Put(string key, V value);

        /// <summary>
        /// Returns the item in the mapping at the given key. 
        /// </summary>
        V Get(string key);

        V this[string key] { get; set; }
    }
}