namespace Stratis.SmartContracts
{
    public interface ISmartContractMapping<V>
    {
        /// <summary>
        /// Store an item in the mapping at the given key. In PersistentState, the given item will be stored at {name}[{key}].
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void Put(string key, V value);

        /// <summary>
        /// Returns the item in the mapping at the given key. 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        V Get(string key);

        V this[string key] { get; set; }
    }
}