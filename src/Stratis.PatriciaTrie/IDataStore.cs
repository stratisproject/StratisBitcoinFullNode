namespace Stratis.PatriciaTrie
{
    public interface IDataStore
    {
        void Put(byte[] key, byte[] val);
        byte[] Get(byte[] key);
        void Delete(byte[] key);
    }
}
