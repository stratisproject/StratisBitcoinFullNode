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
    internal class SmartContractTransactionExecutor
    {
        private readonly IContractStateRepository stateRepository;
        private readonly IContractStateRepository nestedStateRepository;
        private readonly SmartContractDecompiler decompiler;
        private readonly SmartContractValidator validator;
        private readonly SmartContractGasInjector gasInjector;
        private readonly SmartContractCarrier smartContractCarrier;
        private readonly ulong height;
        private readonly ulong difficulty;
        private readonly uint160 coinbaseAddress;

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
            // TODO: Get actual address
            uint160 contractAddress = this.smartContractCarrier.GetNewContractAddress();

            this.stateRepository.CreateAccount(0);

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

                byte[] gasAwareExecutionCode = ms.ToArray();

                var persistentState = new PersistentState(this.nestedStateRepository, contractAddress);
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

                SmartContractExecutionResult result = vm.ExecuteMethod(gasAwareExecutionCode.ToArray(), decompilation.ContractType.Name, initMethod?.Name, executionContext);
                // do something with gas

                if (result.Revert)
                {
                    this.nestedStateRepository.Rollback();
                    return result;
                }

                // To start with, no value transfers on create. Can call other contracts but send 0 only.

                this.nestedStateRepository.SetCode(contractAddress, gasAwareExecutionCode);
                this.nestedStateRepository.Commit();
                return result;
            }
        }

        private SmartContractExecutionResult ExecuteCall()
        {
            byte[] contractCode = this.stateRepository.GetCode(this.smartContractCarrier.To);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(contractCode); // This is overkill here. Just for testing atm.

            // YO! VERY IMPORTANT! 

            // Make sure that somewhere around here we check that the method being called ISN'T the SmartContractInit method, or we're in trouble

            uint160 contractAddress = this.smartContractCarrier.To;

            var persistentState = new PersistentState(this.nestedStateRepository, contractAddress);
            this.nestedStateRepository.CurrentTx = this.smartContractCarrier;

            ReflectionVirtualMachine vm = new ReflectionVirtualMachine(persistentState);
            SmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode,
                decomp.ContractType.Name,
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
                  ));

            if (result.Revert)
            {
                this.nestedStateRepository.Rollback();
                return result;
            }

            // We need to append a condensing transaction to the block here if funds are moved.
            IList<TransferInfo> transfers = this.nestedStateRepository.Transfers;
            if (transfers.Any() || this.smartContractCarrier.TxOutValue > 0)
            {
                CondensingTx condensingTx = new CondensingTx(this.smartContractCarrier, transfers, this.nestedStateRepository);
                result.InternalTransactions.Add(condensingTx.CreateCondensingTransaction());
            }
            this.nestedStateRepository.Transfers.Clear();
            this.nestedStateRepository.Commit();

            return result;
        }
    }
}