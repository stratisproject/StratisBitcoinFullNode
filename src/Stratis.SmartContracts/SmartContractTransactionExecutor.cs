using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NBitcoin;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts
{
    public sealed class SmartContractTransactionExecutor
    {
        private readonly SmartContractDecompiler decompiler;
        private readonly SmartContractGasInjector gasInjector;
        private readonly SmartContractCarrier smartContractCarrier;
        private readonly SmartContractValidator validator;

        private readonly IContractStateRepository stateRepository;
        private readonly IContractStateRepository nestedStateRepository;

        private readonly uint160 coinbaseAddress;
        private readonly ulong difficulty;
        private readonly ulong height;

        public SmartContractTransactionExecutor(
            IContractStateRepository stateRepository,
            SmartContractDecompiler smartContractDecompiler,
            SmartContractValidator smartContractValidator,
            SmartContractGasInjector smartContractGasInjector,
            SmartContractCarrier smartContractCarrier,
            ulong height,
            ulong difficulty,
            uint160 coinbaseAddress)
        {
            this.stateRepository = stateRepository;
            this.nestedStateRepository = stateRepository.StartTracking();
            this.decompiler = smartContractDecompiler;
            this.validator = smartContractValidator;
            this.gasInjector = smartContractGasInjector;
            this.smartContractCarrier = smartContractCarrier;
            this.height = height;
            this.difficulty = difficulty;
            this.coinbaseAddress = coinbaseAddress;
        }

        public SmartContractExecutionResult Execute()
        {
            return (this.smartContractCarrier.OpCodeType == OpcodeType.OP_CREATECONTRACT) ? ExecuteCreate() : ExecuteCall();
        }

        private SmartContractExecutionResult ExecuteCreate()
        {
            uint160 contractAddress = this.smartContractCarrier.GetNewContractAddress();

            this.stateRepository.CreateAccount(contractAddress);

            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(this.smartContractCarrier.ContractExecutionCode);
            SmartContractValidationResult validationResult = this.validator.ValidateContract(decompilation);

            if (!validationResult.Valid)
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
                IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.nestedStateRepository, gasMeter);
                var persistentState = new PersistentState(this.nestedStateRepository, persistenceStrategy, contractAddress);
                var vm = new ReflectionVirtualMachine(persistentState);

                MethodDefinition initMethod = decompilation.ContractType.Methods.FirstOrDefault(x => x.CustomAttributes.Any(y => y.AttributeType.FullName == typeof(SmartContractInitAttribute).FullName));

                var executionContext = new SmartContractExecutionContext
                    (
                        new Block(this.height, this.coinbaseAddress, this.difficulty),
                        new Message(
                            new Address(contractAddress),
                            new Address(this.smartContractCarrier.Sender),
                            this.smartContractCarrier.TxOutValue,
                            this.smartContractCarrier.GasLimit
                            ),
                        this.smartContractCarrier.GasUnitPrice,
                        this.smartContractCarrier.MethodParameters
                    );

                SmartContractExecutionResult result = vm.ExecuteMethod(
                    contractCode.ToArray(), 
                    decompilation.ContractType.Name, 
                    initMethod?.Name, 
                    executionContext,
                    gasMeter);

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

        private SmartContractExecutionResult ExecuteCall()
        {
            byte[] contractCode = this.stateRepository.GetCode(this.smartContractCarrier.To);
            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(contractCode);

            // YO! VERY IMPORTANT! 

            // Make sure that somewhere around here we check that the method being called ISN'T the SmartContractInit method, or we're in trouble
   
            this.gasInjector.AddGasCalculationToContract(decompilation.ContractType, decompilation.BaseType);

            using (var ms = new MemoryStream())
            {
                decompilation.ModuleDefinition.Write(ms);

                byte[] contractCodeWGas = ms.ToArray();

                uint160 contractAddress = this.smartContractCarrier.To;

                GasMeter gasMeter = new GasMeter(this.smartContractCarrier.GasLimit);
                IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.nestedStateRepository, gasMeter);

                var persistentState = new PersistentState(this.nestedStateRepository, persistenceStrategy, contractAddress);
                this.nestedStateRepository.CurrentCarrier = this.smartContractCarrier;
                ReflectionVirtualMachine vm = new ReflectionVirtualMachine(persistentState);
                SmartContractExecutionResult result = vm.ExecuteMethod(
                    contractCodeWGas,
                    decompilation.ContractType.Name,
                    this.smartContractCarrier.MethodName,
                    new SmartContractExecutionContext
                    (
                          new Block(Convert.ToUInt64(this.height), this.coinbaseAddress, Convert.ToUInt64(this.difficulty)),
                          new Message(
                              new Address(contractAddress),
                              new Address(this.smartContractCarrier.Sender),
                              this.smartContractCarrier.TxOutValue,
                              this.smartContractCarrier.GasLimit
                          ),
                          this.smartContractCarrier.GasUnitPrice,
                          this.smartContractCarrier.MethodParameters
                      ),
                    gasMeter);

                return result.Revert ? RevertExecution(result) : CommitExecution(result);
            }
        }

        /// <summary>
        /// Contract execution completed successfully, commit state.
        /// <para>
        /// We need to append a condensing transaction to the block if funds are moved.
        /// </para>
        /// </summary>
        private SmartContractExecutionResult CommitExecution(SmartContractExecutionResult executionResult)
        {
            IList<TransferInfo> transfers = this.nestedStateRepository.Transfers;
            if (transfers.Any() || this.smartContractCarrier.TxOutValue > 0)
            {
                var condensingTx = new CondensingTx(this.smartContractCarrier, transfers, this.nestedStateRepository);
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
        private SmartContractExecutionResult RevertExecution(SmartContractExecutionResult executionResult)
        {
            if (this.smartContractCarrier.TxOutValue > 0)
            {
                Transaction tx = new CondensingTx(this.smartContractCarrier).CreateRefundTransaction();
                executionResult.InternalTransactions.Add(tx);
            }

            this.nestedStateRepository.Rollback();

            return executionResult;
        }
    }
}