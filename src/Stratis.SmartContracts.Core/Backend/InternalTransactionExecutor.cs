using NBitcoin;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Core.Backend
{
    ///<inheritdoc/>
    public sealed class InternalTransactionExecutor : IInternalTransactionExecutor
    {
        private readonly IContractStateRepository contractStateRepository;
        private readonly Network network;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly InternalTransferList internalTransferList;

        public InternalTransactionExecutor(
            IContractStateRepository contractStateRepository, 
            Network network, 
            IKeyEncodingStrategy keyEncodingStrategy,
            InternalTransferList internalTransferList)
        {
            this.contractStateRepository = contractStateRepository;
            this.network = network;
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.internalTransferList = internalTransferList;
        }

        ///<inheritdoc/>
        public ITransferResult TransferFunds(ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer, TransferFundsToContract contractDetails)
        {
            // TODO: The act of calling this should cost a lot of gas!
            var balance = smartContractState.GetBalance();
            if (balance < amountToTransfer)
                throw new InsufficientBalanceException();

            // Discern whether this is a contract or an ordinary address.
            byte[] contractCode = this.contractStateRepository.GetCode(addressTo.ToUint160(this.network));

            if (contractCode == null || contractCode.Length == 0)
            {
                // If it is not a contract, just record the transfer and return.
                this.internalTransferList.Add(new TransferInfo
                {
                    From = smartContractState.Message.ContractAddress.ToUint160(this.network),
                    To = addressTo.ToUint160(this.network),
                    Value = amountToTransfer
                });

                return TransferResult.Empty();
            }

            return ExecuteTransferFundsToContract(contractCode, smartContractState, addressTo, amountToTransfer, contractDetails);
        }

        /// <summary>
        /// If the address to where the funds will be tranferred to is a contract, instantiate and execute it.
        /// </summary>
        private ITransferResult ExecuteTransferFundsToContract(byte[] contractCode, ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer, TransferFundsToContract contractDetails)
        {
            IContractStateRepository track = this.contractStateRepository.StartTracking();
            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(track, smartContractState.GasMeter, this.keyEncodingStrategy);
            IPersistentState newPersistentState = new PersistentState(persistenceStrategy, addressTo.ToUint160(this.network), this.network);

            var newMessage = new Message(addressTo, smartContractState.Message.ContractAddress, amountToTransfer, (Gas)(smartContractState.Message.GasLimit - smartContractState.GasMeter.GasConsumed));

            ISmartContractExecutionContext newContext = new SmartContractExecutionContext(smartContractState.Block, newMessage, addressTo.ToUint160(this.network), 0, contractDetails.MethodParameters);

            ISmartContractVirtualMachine vm = new ReflectionVirtualMachine(newPersistentState, new InternalTransactionExecutorFactory(this.network, this.keyEncodingStrategy), track);

            ISmartContractExecutionResult executionResult = vm.ExecuteMethod(
                contractCode,
                contractDetails.ContractMethodName,
                newContext,
                smartContractState.GasMeter);

            smartContractState.GasMeter.Spend(executionResult.GasConsumed);

            if (executionResult.Revert)
            {
                track.Rollback();
                return TransferResult.Failed(executionResult.Exception);
            }

            track.Commit();

            this.internalTransferList.Add(new TransferInfo
            {
                From = smartContractState.Message.ContractAddress.ToUint160(this.network),
                To = addressTo.ToUint160(this.network),
                Value = amountToTransfer
            });
            
            return TransferResult.Transferred(executionResult.Return);
        }
    }
}