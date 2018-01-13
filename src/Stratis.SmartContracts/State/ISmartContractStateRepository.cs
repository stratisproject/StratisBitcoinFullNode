using NBitcoin;

namespace Stratis.SmartContracts.State
{
    public interface ISmartContractStateRepository
    {
        AccountState CreateAccount(uint160 address);

        void SetObject<T>(uint160 address, object key, T toStore);
        T GetObject<T>(uint160 address, object key);

        byte[] GetCode(uint160 address);
        void SetCode(uint160 address, byte[] code);

        void Rollback(uint256 hash);
        void Commit();
    }
}
