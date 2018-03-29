using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NBitcoin;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Core
{
    public sealed class SmartContractTransactionExecutor
    {
        private readonly SmartContractDecompiler decompiler;
        private readonly ISmartContractGasInjector gasInjector;
        private readonly SmartContractCarrier smartContractCarrier;
        private readonly SmartContractValidator validator;

        private readonly IContractStateRepository stateRepository;
        private readonly IContractStateRepository nestedStateRepository;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;

        private readonly uint160 coinbaseAddress;
        private readonly ulong height;

        private readonly Network network;

        public SmartContractTransactionExecutor(
            IContractStateRepository stateRepository,
            SmartContractDecompiler smartContractDecompiler,
            SmartContractValidator smartContractValidator,
            ISmartContractGasInjector smartContractGasInjector,
            SmartContractCarrier smartContractCarrier,
            IKeyEncodingStrategy keyEncodingStrategy,
            ulong height,
            uint160 coinbaseAddress,
            Network network)
        {
            this.stateRepository = stateRepository;
            this.nestedStateRepository = stateRepository.StartTracking();
            this.decompiler = smartContractDecompiler;
            this.validator = smartContractValidator;
            this.gasInjector = smartContractGasInjector;
            this.smartContractCarrier = smartContractCarrier;
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.height = height;
            this.coinbaseAddress = coinbaseAddress;
            this.network = network;
        }

        public ISmartContractExecutionResult Execute()
        {
            return (this.smartContractCarrier.OpCodeType == OpcodeType.OP_CREATECONTRACT) ? this.ExecuteCreate() : this.ExecuteCall();
        }

        private ISmartContractExecutionResult ExecuteCreate()
        {
            uint160 contractAddress = this.smartContractCarrier.GetNewContractAddress();

            this.stateRepository.CreateAccount(contractAddress);

            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(this.smartContractCarrier.ContractExecutionCode);
            SmartContractValidationResult validationResult = this.validator.ValidateContract(decompilation);

            if (!validationResult.IsValid)
            {
                // TODO: Expend all of users fee - no deployment
                throw new NotImplementedException();
            }

            this.gasInjector.AddGasCalculationToContract(decompilation.ContractType, decompilation.BaseType);

            using (var ms = new MemoryStream())
            {
                decompilation.ModuleDefinition.Write(ms);

                byte[] contractCode = ms.ToArray();

                GasMeter gasMeter = new GasMeter(this.smartContractCarrier.GasLimit);
                IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.nestedStateRepository, gasMeter, this.keyEncodingStrategy);
                var persistentState = new PersistentState(this.nestedStateRepository, persistenceStrategy, contractAddress, this.network);
                var vm = new ReflectionVirtualMachine(persistentState);

                MethodDefinition initMethod = decompilation.ContractType.Methods.FirstOrDefault(x => x.CustomAttributes.Any(y => y.AttributeType.FullName == typeof(SmartContractInitAttribute).FullName));

                var executionContext = new SmartContractExecutionContext
                    (
                        new Block(this.height, this.coinbaseAddress.ToAddress(this.network)),
                        new Message(
                            contractAddress.ToAddress(this.network),
                            this.smartContractCarrier.Sender.ToAddress(this.network),
                            this.smartContractCarrier.TxOutValue,
                            this.smartContractCarrier.GasLimit
                            ),
                        this.smartContractCarrier.GasPrice,
                        this.smartContractCarrier.MethodParameters
                    );

                var internalTransactionExecutor = new InternalTransactionExecutor(this.nestedStateRepository, this.network, this.keyEncodingStrategy);
                Func<ulong> getBalance = () => this.nestedStateRepository.GetCurrentBalance(contractAddress);

                ISmartContractExecutionResult result = vm.ExecuteMethod(
                    contractCode.ToArray(),
                    decompilation.ContractType.Name,
                    initMethod?.Name,
                    executionContext,
                    gasMeter,
                    internalTransactionExecutor,
                    getBalance);

                if (result.Revert)
                {
                    this.nestedStateRepository.Rollback();
                    return result;
                }

                // To start with, no value transfers on create. Can call other contracts but send 0 only.

                this.nestedStateRepository.SetCode(contractAddress, this.smartContractCarrier.ContractExecutionCode);
                this.nestedStateRepository.Commit();
                return result;
            }
        }

        private ISmartContractExecutionResult ExecuteCall()
        {
            byte[] contractCode = this.stateRepository.GetCode(this.smartContractCarrier.ContractAddress);
            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(contractCode);

            // YO! VERY IMPORTANT! 

            // Make sure that somewhere around here we check that the method being called ISN'T the SmartContractInit method, or we're in trouble

            this.gasInjector.AddGasCalculationToContract(decompilation.ContractType, decompilation.BaseType);

            using (var ms = new MemoryStream())
            {
                decompilation.ModuleDefinition.Write(ms);

                byte[] contractCodeWGas = ms.ToArray();

                uint160 contractAddress = this.smartContractCarrier.ContractAddress;

                GasMeter gasMeter = new GasMeter(this.smartContractCarrier.GasLimit);
                IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.nestedStateRepository, gasMeter, this.keyEncodingStrategy);
                var internalTransactionExecutor = new InternalTransactionExecutor(this.nestedStateRepository, this.network, this.keyEncodingStrategy);
                Func<ulong> getBalance = () => this.nestedStateRepository.GetCurrentBalance(contractAddress);

                var persistentState = new PersistentState(this.nestedStateRepository, persistenceStrategy, contractAddress, this.network);
                this.nestedStateRepository.CurrentCarrier = this.smartContractCarrier;
                var vm = new ReflectionVirtualMachine(persistentState);

                var executionContext = new SmartContractExecutionContext
                (
                    new Block(Convert.ToUInt64(this.height), this.coinbaseAddress.ToAddress(this.network)),
                    new Message(
                        contractAddress.ToAddress(this.network),
                        this.smartContractCarrier.Sender.ToAddress(this.network),
                        this.smartContractCarrier.TxOutValue,
                        this.smartContractCarrier.GasLimit
                        ),
                    this.smartContractCarrier.GasPrice,
                    this.smartContractCarrier.MethodParameters);

                ISmartContractExecutionResult result = vm.ExecuteMethod(
                    contractCodeWGas,
                    decompilation.ContractType.Name,
                    this.smartContractCarrier.MethodName,
                    executionContext,
                    gasMeter,
                    internalTransactionExecutor,
                    getBalance);

                return result.Revert ? this.RevertExecution(result) : this.CommitExecution(result);
            }
        }

        /// <summary>
        /// Contract execution completed successfully, commit state.
        /// <para>
        /// We need to append a condensing transaction to the block if funds are moved.
        /// </para>
        /// </summary>
        private ISmartContractExecutionResult CommitExecution(ISmartContractExecutionResult executionResult)
        {
            IList<TransferInfo> transfers = this.nestedStateRepository.Transfers;
            if (transfers.Any() || this.smartContractCarrier.TxOutValue > 0)
            {
                var condensingTx = new CondensingTx(this.smartContractCarrier, transfers, this.nestedStateRepository, this.network);
                executionResult.InternalTransactions.Add(condensingTx.CreateCondensingTransaction());
            }

            this.nestedStateRepository.Transfers.Clear();
            this.nestedStateRepository.Commit();

            return executionResult;
        }

        /// <summary>
        /// Contract execution failed, therefore we need to revert state.
        /// <para>
        /// If funds was send to the contract, we need to send it back to the sender.
        /// </para>
        /// </summary>
        private ISmartContractExecutionResult RevertExecution(ISmartContractExecutionResult executionResult)
        {
            if (this.smartContractCarrier.TxOutValue > 0)
            {
                Transaction tx = new CondensingTx(this.smartContractCarrier, this.network).CreateRefundTransaction();
                executionResult.InternalTransactions.Add(tx);
            }

            this.nestedStateRepository.Rollback();

            return executionResult;
        }
    }
}