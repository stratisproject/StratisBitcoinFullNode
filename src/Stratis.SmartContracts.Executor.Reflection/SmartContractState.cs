using System;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Smart contract state that gets injected into the smart contract by the <see cref="ReflectionVirtualMachine"/>.
    /// </summary>
    /// <remarks>
    /// TODO: SmartContractState is basically the same thing as <see cref="SmartContractExecutionContext"/> so merge them eventually.
    /// </remarks>
    public sealed class SmartContractState : ISmartContractState
    {
        public SmartContractState(
            IBlock block,
            IMessage message,
            IPersistentState persistentState,
            IGasMeter gasMeter,
            IInternalTransactionExecutor internalTransactionExecutor,
            IInternalHashHelper internalHashHelper,
            Func<ulong> getBalance)
        {
            this.Block = block;
            this.Message = message;
            this.PersistentState = persistentState;
            this.GasMeter = gasMeter;
            this.InternalTransactionExecutor = internalTransactionExecutor;
            this.InternalHashHelper = internalHashHelper;
            this.GetBalance = getBalance;
        }

        public IBlock Block { get; }

        public IMessage Message { get; }

        public IPersistentState PersistentState { get; }

        public IGasMeter GasMeter { get; }

        public IInternalTransactionExecutor InternalTransactionExecutor { get; }

        public IInternalHashHelper InternalHashHelper { get; }

        public Func<ulong> GetBalance { get; }
    }
}