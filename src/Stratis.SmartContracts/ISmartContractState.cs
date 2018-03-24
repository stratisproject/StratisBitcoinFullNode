using System;

namespace Stratis.SmartContracts
{
    public interface ISmartContractState
    {
        Block Block { get; }
        Message Message { get; }
        IPersistentState PersistentState { get; }
        IGasMeter GasMeter { get; }
        IInternalTransactionExecutor InternalTransactionExecutor { get; }
        IInternalHashHelper InternalHashHelper { get; }
        Func<ulong> GetBalance { get; }
    }
}