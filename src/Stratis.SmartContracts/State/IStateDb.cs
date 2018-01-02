using NBitcoin;

namespace Stratis.SmartContracts.State
{
    // TODO: When putting addresses into database, use UInt180 or similar
    // Also TODO: rollback / commit functionality with DBreeze
    internal interface IStateDb
    {
        AccountState CreateAccount(uint160 address);

        void SetObject<T>(uint160 address, string key, T toStore);
        T GetObject<T>(uint160 address, string key);

        void SetObject<T>(uint160 address, object key, T toStore);
        T GetObject<T>(uint160 address, object key);

        byte[] GetCode(uint160 address);
        void SetCode(uint160 address, byte[] code);

        ulong GetBalance(uint160 address);
        ulong AddBalance(uint160 address, ulong value);
        ulong SubtractBalance(uint160 address, ulong value);

        void IncrementNonce(uint160 address);
        ulong GetNonce(uint160 address);

        void Rewind();
        void Commit();
    }
}
