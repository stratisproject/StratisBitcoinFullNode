using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.State
{
    public interface IRepository
    {
        AccountState CreateAccount(uint160 addr);
        bool IsExist(uint160 addr);
        AccountState GetAccountState(uint160 addr);
        void Delete(uint160 addr);
        // ContractDetails getContractDetails(byte[] addr);
        // boolean hasContractDetails(byte[] addr);
        void SaveCode(uint160 addr, byte[] code);
        byte[] GetCode(uint160 addr);
        byte[] GetCodeHash(uint160 addr);
        void AddStorageRow(uint160 addr, byte[] key, byte[] value);
        byte[] GetStorageValue(uint160 addr, byte[] key);
        HashSet<uint160> GetAccountsKeys();
        IRepository StartTracking();
        void Flush();
        void FlushNoReconnect();
        void Commit();
        void Rollback();
        void SyncToRoot(byte[] root);
        bool IsClosed();
        void Close();
        void Reset();
        byte[] GetRoot();
        IRepository GetSnapshotTo(byte[] root);
    }
}
