using System.IO;
using NBitcoin;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core.Backend
{
    ///<inheritdoc/>
    public sealed class InternalTransactionExecutor : IInternalTransactionExecutor
    {
        private readonly IContractStateRepository constractStateRepository;
        private readonly Network network;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;

        public InternalTransactionExecutor(IContractStateRepository constractStateRepository, Network network, IKeyEncodingStrategy keyEncodingStrategy)
        {
            this.constractStateRepository = constractStateRepository;
            this.network = network;
            this.keyEncodingStrategy = keyEncodingStrategy;
        }

        private static byte[] AddGasToContractExecutionCode(SmartContractDecompilation decompilation)
        {
            using (var memoryStream = new MemoryStream())
            {
                SmartContractGasInjector.AddGasCalculationToContract(decompilation.ContractType, decompilation.BaseType);
                decompilation.ModuleDefinition.Write(memoryStream);
                return memoryStream.ToArray();
            }
        }

        ///<inheritdoc/>
        public ITransferResult TransferFunds(ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer, TransferFundsToContract contractDetails)
        {
            // TODO: The act of calling this should cost a lot of gas!
            var balance = smartContractState.GetBalance();
            if (balance < amountToTransfer)
                throw new InsufficientBalanceException();

            // Discern whether this is a contract or an ordinary address.
            byte[] contractCode = this.constractStateRepository.GetCode(addressTo.ToUint160(this.network));

            if (contractCode == null || contractCode.Length == 0)
            {
                // If it is not a contract, just record the transfer and return.
                this.constractStateRepository.TransferBalance(smartContractState.Message.ContractAddress.ToUint160(this.network), addressTo.ToUint160(this.network), amountToTransfer);
                return TransferResult.Empty();
            }

            return ExecuteTransferFundsToContract(contractCode, smartContractState, addressTo, amountToTransfer, contractDetails);
        }

        /// <summary>
        /// If the address to where the funds will be tranferred to is a contract, instantiate and execute it.
        /// </summary>
        private ITransferResult ExecuteTransferFundsToContract(byte[] contractCode, ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer, TransferFundsToContract contractDetails)
        {
            IContractStateRepository track = this.constractStateRepository.StartTracking();
            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(track, smartContractState.GasMeter, this.keyEncodingStrategy);
            IPersistentState newPersistentState = new PersistentState(track, persistenceStrategy, addressTo.ToUint160(this.network), this.network);

            var newMessage = new Message(addressTo, smartContractState.Message.ContractAddress, amountToTransfer, (Gas)(smartContractState.Message.GasLimit - smartContractState.GasMeter.GasConsumed));

            ISmartContractExecutionContext newContext = new SmartContractExecutionContext(smartContractState.Block, newMessage, 0, contractDetails.MethodParameters);

            var gasInjectedContractCode =
                AddGasToContractExecutionCode(SmartContractDecompiler.GetModuleDefinition(contractCode));

            ISmartContractVirtualMachine vm = new ReflectionVirtualMachine(newPersistentState);

            ISmartContractExecutionResult executionResult = vm.ExecuteMethod(
                gasInjectedContractCode,
                contractDetails.ContractTypeName,
                contractDetails.ContractMethodName,
                newContext,
                smartContractState.GasMeter,
                this,
                smartContractState.GetBalance);

            smartContractState.GasMeter.Spend(executionResult.GasConsumed);

            if (executionResult.Revert)
            {
                track.Rollback();
                return TransferResult.Failed(executionResult.Exception);
            }

            track.Commit();

            this.constractStateRepository.TransferBalance(smartContractState.Message.ContractAddress.ToUint160(this.network), addressTo.ToUint160(this.network), amountToTransfer);

            return TransferResult.Transferred(executionResult.Return);
        }
    }
}