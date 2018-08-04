namespace Stratis.SmartContracts
{
    public interface IScList<T>
    {
        /// <summary>
        /// Store an item in the mapping at the given key. In PersistentState, the given item will be stored at {name}[{key}].
        /// </summary>
        void Put(int key, T value);

        /// <summary>
        /// Returns the item in the mapping at the given key. 
        /// </summary>
        T Get(int key);

        T this[int key] { get; set; }

        int Count { get; }

        void Push(T value);
    }
}
