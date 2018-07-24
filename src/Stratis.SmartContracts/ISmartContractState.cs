using System;

namespace Stratis.SmartContracts
{
    public interface ISmartContractState
    {
        IBlock Block { get; }
        IMessage Message { get; }
        IPersistentState PersistentState { get; }
        IGasMeter GasMeter { get; }
        IInternalTransactionExecutor InternalTransactionExecutor { get; }
        IInternalHashHelper InternalHashHelper { get; }
        Func<ulong> GetBalance { get; }
    }
}