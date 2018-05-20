namespace Stratis.Patricia
{
    public interface IPatriciaTrie : ISource<byte[],byte[]>
    {
        byte[] GetRootHash();
        void SetRootHash(byte[] root);
    }
}
