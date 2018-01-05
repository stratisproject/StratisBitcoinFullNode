using NBitcoin;

namespace Stratis.SmartContracts.State
{
    // TODO: When putting addresses into database, use UInt180 or similar
    // Also TODO: rollback / commit functionality with DBreeze
    public interface ISmartContractStateRepository
    {
        AccountState CreateAccount(uint160 address);

        //void SetObject<T>(uint160 address, string key, T toStore);
        //T GetObject<T>(uint160 address, string key);

        void SetObject<T>(uint160 address, object key, T toStore);
        T GetObject<T>(uint160 address, object key);

        byte[] GetCode(uint160 address);
        void SetCode(uint160 address, byte[] code);
    }
}
