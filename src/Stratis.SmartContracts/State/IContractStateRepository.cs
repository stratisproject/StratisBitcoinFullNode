using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.State
{
    public interface IContractStateRepository
    {
        AccountState CreateAccount(uint160 addr);
        bool IsExist(uint160 addr);
        AccountState GetAccountState(uint160 addr);
        void Delete(uint160 addr);
        void SaveCode(uint160 addr, byte[] code);
        byte[] GetCode(uint160 addr);
        byte[] GetCodeHash(uint160 addr);
        void AddStorageRow(uint160 addr, byte[] key, byte[] value);
        byte[] GetStorageValue(uint160 addr, byte[] key);
        IContractStateRepository StartTracking();
        void Flush();
        void Commit();
        void Rollback();
        void SyncToRoot(byte[] root);
        byte[] GetRoot();
        IContractStateRepository GetSnapshotTo(byte[] root);
    }
}
