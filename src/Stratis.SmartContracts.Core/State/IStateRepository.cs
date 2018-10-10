using NBitcoin;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Core.State
{
    public interface IBalanceRepository
    {
        ulong GetCurrentBalance(uint160 address);
    }

    public interface IStateRepository : IBalanceRepository
    {
        AccountState CreateAccount(uint160 addr);
        bool IsExist(uint160 addr);
        AccountState GetAccountState(uint160 addr);
        void SetCode(uint160 addr, byte[] code);
        byte[] GetCode(uint160 addr);
        byte[] GetCodeHash(uint160 addr);
        void SetStorageValue(uint160 addr, byte[] key, byte[] value);
        byte[] GetStorageValue(uint160 addr, byte[] key);
        string GetContractType(uint160 addr);
        void SetContractType(uint160 addr, string type);
        IStateRepository StartTracking();
        void Flush();
        void Commit();
        void Rollback();
        IStateRepositoryRoot GetSnapshotTo(byte[] stateRoot);

        #region Account Abstraction Layer        

        ContractUnspentOutput GetUnspent(uint160 address);
        void SetUnspent(uint160 address, ContractUnspentOutput vin);
        byte[] GetUnspentHash(uint160 address);
        void ClearUnspent(uint160 address);
        #endregion
    }
}