namespace Stratis.PatriciaTrie
{
    public interface IPatriciaTrie : IDataStore
    {
        byte[] GetRootHash();
        void SetRootHash(byte[] root);
        
    }
}
