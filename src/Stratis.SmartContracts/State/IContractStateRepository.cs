using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.State
{
    public interface IContractStateRepository
    {
        AccountState CreateAccount(uint160 addr);
        bool IsExist(uint160 addr);
        AccountState GetAccountState(uint160 addr);
        void Delete(uint160 addr);
        void SetCode(uint160 addr, byte[] code);
        byte[] GetCode(uint160 addr);
        byte[] GetCodeHash(uint160 addr);
        void SetStorageValue(uint160 addr, byte[] key, byte[] value);
        byte[] GetStorageValue(uint160 addr, byte[] key);
        IContractStateRepository StartTracking();
        void Flush();
        void Commit();
        void Rollback();
        void SyncToRoot(byte[] root);
        byte[] GetRoot();
        IContractStateRepository GetSnapshotTo(byte[] root);

        #region Account Abstraction Layer
        void TransferBalance(uint160 from, uint160 to, ulong value);
        //Vin Vin(uint160 address);
        IList<TransferInfo> GetTransfers();
        #endregion
    }
}
