namespace Stratis.SmartContracts
{
    public interface IScMapping<T>
    {
        /// <summary>
        /// Store an item in the mapping at the given key. In PersistentState, the given item will be stored at {name}[{key}].
        /// </summary>
        void Put(string key, T value);

        /// <summary>
        /// Returns the item in the mapping at the given key. 
        /// </summary>
        T Get(string key);

        T this[string key] { get; set; }
    }
}
