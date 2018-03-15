using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Handles the execution of transactions that happen internally
    /// to a SmartContract, eg. a Transfer of funds to another contract.
    /// </summary>
    public class InternalTransactionExecutor : IInternalTransactionExecutor
    {
        private readonly IContractStateRepository stateRepository;

        public InternalTransactionExecutor(IContractStateRepository stateRepository)
        {
            this.stateRepository = stateRepository;
        }

        public ITransferResult Transfer(ISmartContractState state, Address addressTo, ulong amount, TransactionDetails transactionDetails)
        {
            //TODO: The act of calling this should cost a lot of gas!

            if (state.GetBalance() < amount)
                throw new InsufficientBalanceException();

            // Discern whether is a contract or ordinary address.
            byte[] contractCode = this.stateRepository.GetCode(addressTo.ToUint160());

            if (contractCode == null || contractCode.Length == 0)
            {
                // Is not a contract, so just record the transfer and return
                this.stateRepository.TransferBalance(state.Message.ContractAddress.ToUint160(), addressTo.ToUint160(), amount);
                return new TransferResult();
            }

            // It's a contract - instantiate the contract and execute.
            IContractStateRepository track = this.stateRepository.StartTracking();
            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(track, state.GasMeter);
            IPersistentState newPersistentState = new PersistentState(track, persistenceStrategy, addressTo.ToUint160());
            Message newMessage = new Message(addressTo, state.Message.ContractAddress, amount, (Gas)(state.Message.GasLimit - state.GasMeter.ConsumedGas));
            ISmartContractExecutionContext newContext = new SmartContractExecutionContext(state.Block, newMessage, 0, transactionDetails.Parameters);
            ISmartContractVirtualMachine vm = new ReflectionVirtualMachine(newPersistentState);
            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode,
                transactionDetails.ContractTypeName,
                transactionDetails.ContractMethodName,
                newContext,
                state.GasMeter,
                this,
                state.GetBalance);

            state.GasMeter.Spend(result.GasUnitsUsed);

            if (result.Revert)
            {
                // contract execution unsuccessful
                track.Rollback();
                return new TransferResult(null, result.Exception);
            }

            track.Commit();
            this.stateRepository.TransferBalance(state.Message.ContractAddress.ToUint160(), addressTo.ToUint160(), amount);
            return new TransferResult(result.Return, null);
        }
    }
}
