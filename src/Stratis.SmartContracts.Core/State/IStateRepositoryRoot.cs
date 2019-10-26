namespace Stratis.SmartContracts.Core.State
{
    public interface IStateRepositoryRoot : IStateRepository
    {
        byte[] Root { get; }

        void SyncToRoot(byte[] root);
    }
}
