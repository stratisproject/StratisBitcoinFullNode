namespace Stratis.Patricia
{
    /// <summary>
    /// A data store.
    /// </summary>
    public interface ISource<K,V>
    {
        /// <summary>
        /// Store a value by the given key.
        /// </summary>
        void Put(K key, V val);

        /// <summary>
        /// Retrieve a value by key.
        /// </summary>
        V Get(K key);

        /// <summary>
        /// Remove a key and value from the data store.
        /// </summary>
        void Delete(K key);

        /// <summary>
        /// Persist any changes to an underlying source.
        /// </summary>
        /// <returns></returns>
        bool Flush();
    }
}
