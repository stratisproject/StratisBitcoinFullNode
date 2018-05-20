namespace Stratis.Patricia
{
    public interface ISource<K,V>
    {
        void Put(K key, V val);
        V Get(K key);
        void Delete(K key);
        bool Flush();
    }
}
