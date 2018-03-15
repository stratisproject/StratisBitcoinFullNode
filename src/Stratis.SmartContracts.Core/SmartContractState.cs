using System;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// TODO - SmartContractState is basically the same thing as SmartContractExecutionContext so merge them eventually
    /// </summary>
    public class SmartContractState : ISmartContractState
    {
        public SmartContractState(
            Block block, 
            Message message, 
            IPersistentState persistentState, 
            IGasMeter gasMeter,
            IInternalTransactionExecutor internalTransactionExecutor,
            Func<ulong> getBalance)
        {
            this.Block = block;
            this.Message = message;
            this.PersistentState = persistentState;
            this.GasMeter = gasMeter;
            this.InternalTransactionExecutor = internalTransactionExecutor;
            this.GetBalance = getBalance;
        }

        public Block Block { get; }

        public Message Message { get; }

        public IPersistentState PersistentState { get; }

        public IGasMeter GasMeter { get; }

        public IInternalTransactionExecutor InternalTransactionExecutor { get; }

        public Func<ulong> GetBalance { get; }
    }
}