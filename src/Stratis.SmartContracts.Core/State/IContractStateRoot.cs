namespace Stratis.SmartContracts.Core.State
{
    public interface IContractStateRoot : IContractState
    {
        byte[] Root { get; }

        void SyncToRoot(byte[] root);
    }
}
