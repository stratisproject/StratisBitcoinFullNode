using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Executor.Reflection
{
    ///<inheritdoc/>
    public sealed class InternalTransactionExecutor : IInternalTransactionExecutor
    {
        private readonly IContractStateRepository contractStateRepository;
        private readonly List<TransferInfo> internalTransferList;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly ISmartContractVirtualMachine vm;
        private readonly ITransactionContext transactionContext;

        public InternalTransactionExecutor(ITransactionContext transactionContext, ISmartContractVirtualMachine vm,
            IContractStateRepository contractStateRepository,
            List<TransferInfo> internalTransferList,
            IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network)
        {
            this.transactionContext = transactionContext;
            this.contractStateRepository = contractStateRepository;
            this.internalTransferList = internalTransferList;
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.network = network;
            this.vm = vm;
        }

        ///<inheritdoc />
        public ICreateResult Create<T>(ISmartContractState smartContractState, object[] parameters, ulong amountToTransfer)
        {
            throw new System.NotImplementedException();
        }

        ///<inheritdoc />
        public ITransferResult TransferFunds(ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer, TransferFundsToContract contractDetails)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(addressTo), addressTo, nameof(amountToTransfer), amountToTransfer);

            // TODO: The act of calling this should cost a lot of gas!
            var balance = smartContractState.GetBalance();
            if (balance < amountToTransfer)
            {
                this.logger.LogTrace("(-)[INSUFFICIENT_BALANCE]:{0}={1}", nameof(balance), balance);
                throw new InsufficientBalanceException();
            }

            // Discern whether this is a contract or an ordinary address.
            byte[] contractCode = this.contractStateRepository.GetCode(addressTo.ToUint160(this.network));
            if (contractCode == null || contractCode.Length == 0)
            {
                this.internalTransferList.Add(new TransferInfo
                {
                    From = smartContractState.Message.ContractAddress.ToUint160(this.network),
                    To = addressTo.ToUint160(this.network),
                    Value = amountToTransfer
                });

                this.logger.LogTrace("(-)[TRANSFER_TO_SENDER]:Transfer {0} from {1} to {2}.", smartContractState.Message.ContractAddress, addressTo, amountToTransfer);
                return TransferResult.Empty();
            }

            this.logger.LogTrace("(-)[TRANSFER_TO_CONTRACT]");

            return ExecuteTransferFundsToContract(contractCode, smartContractState, addressTo, amountToTransfer, contractDetails);
        }

        /// <summary>
        /// If the address to where the funds will be tranferred to is a contract, instantiate and execute it.
        /// </summary>
        private ITransferResult ExecuteTransferFundsToContract(byte[] contractCode, ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer, TransferFundsToContract contractDetails)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(addressTo), addressTo, nameof(amountToTransfer), amountToTransfer);

            IContractStateRepository track = this.contractStateRepository.StartTracking();

            var callData = new CallData(smartContractState.GasMeter.GasLimit, addressTo.ToUint160(this.network), contractDetails.ContractMethodName, contractDetails.MethodParameters);
            
            var context = new TransactionContext(
                this.transactionContext.TransactionHash,
                this.transactionContext.BlockHeight,
                this.transactionContext.Coinbase,
                smartContractState.Message.ContractAddress.ToUint160(this.network),
                amountToTransfer);

            var result = this.vm.ExecuteMethod(smartContractState.GasMeter, 
                track, 
                callData,
                context);

            var revert = result.ExecutionException != null;

            if (revert)
            {
                track.Rollback();
                return TransferResult.Failed(result.ExecutionException);
            }

            track.Commit();

            this.internalTransferList.Add(new TransferInfo
            {
                From = smartContractState.Message.ContractAddress.ToUint160(this.network),
                To = addressTo.ToUint160(this.network),
                Value = amountToTransfer
            });

            this.logger.LogTrace("(-)");

            return TransferResult.Transferred(result.Result);
        }
    }
}