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
        private readonly IContractStateRepository state;
        private readonly IContractStateRepository stateTrack;
        private readonly SmartContractDecompiler decompiler;
        private readonly SmartContractValidator validator;
        private readonly SmartContractGasInjector gasInjector;
        private readonly SmartContractCarrier smartContractCarrier;
        private readonly ulong blockNum;
        private readonly ulong difficulty;
        private readonly uint160 coinbaseAddress;

        public SmartContractTransactionExecutor(IContractStateRepository state,
            SmartContractDecompiler smartContractDecompiler,
            SmartContractValidator smartContractValidator,
            SmartContractGasInjector smartContractGasInjector,
            SmartContractCarrier scTransaction,
            ulong blockNum,
            ulong difficulty,
            uint160 coinbaseAddress)
        {
            this.state = state;
            this.stateTrack = state.StartTracking();
            this.decompiler = smartContractDecompiler;
            this.validator = smartContractValidator;
            this.gasInjector = smartContractGasInjector;
            this.smartContractCarrier = scTransaction;
            this.blockNum = blockNum;
            this.difficulty = difficulty;
            this.coinbaseAddress = coinbaseAddress;
        }

        public SmartContractExecutionResult Execute()
        {
            return (this.smartContractCarrier.OpCodeType == OpcodeType.OP_CREATECONTRACT) ? ExecuteCreate() : ExecuteCall();
        }

        private SmartContractExecutionResult ExecuteCreate()
        {
            uint160 contractAddress = this.smartContractCarrier.GetNewContractAddress(); // TODO: GET ACTUAL NUM
            this.state.CreateAccount(0);

            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(this.smartContractCarrier.ContractExecutionCode);
            SmartContractValidationResult validationResult = this.validator.ValidateContract(decompilation);

            if (!validationResult.Valid)
            {
                // expend all of users fee - no deployment
                throw new NotImplementedException();
            }

            using (var ms = new MemoryStream())
            {
                decompilation.ModuleDefinition.Write(ms);

                byte[] contractCode = ms.ToArray();

                GasMeter gasMeter = new GasMeter(this.smartContractCarrier.GasLimit);

                var persistentState = new PersistentState(this.stateTrack, contractAddress);
                var vm = new ReflectionVirtualMachine(persistentState);

                MethodDefinition initMethod = decompilation.ContractType.Methods.FirstOrDefault(x => x.CustomAttributes.Any(y => y.AttributeType.FullName == typeof(SmartContractInitAttribute).FullName));

                var executionContext = new SmartContractExecutionContext
                    (
                        new Block(this.blockNum, this.coinbaseAddress, this.difficulty),
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
                    this.stateTrack.Rollback();
                    return result;
                }

                // To start with, no value transfers on create. Can call other contracts but send 0 only.

                this.stateTrack.SetCode(contractAddress, contractCode);
                this.stateTrack.Commit();
                return result;
            }
        }

        private SmartContractExecutionResult ExecuteCall()
        {
            byte[] contractCode = this.state.GetCode(this.smartContractCarrier.To);
            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(contractCode); // This is overkill here. Just for testing atm.

            // YO! VERY IMPORTANT! 

            // Make sure that somewhere around here we check that the method being called ISN'T the SmartContractInit method, or we're in trouble

            // Inject gas measurement code before executing        
            this.gasInjector.AddGasCalculationToContract(decompilation.ContractType, decompilation.BaseType);

            uint160 contractAddress = this.smartContractCarrier.To;

            GasMeter gasMeter = new GasMeter(this.smartContractCarrier.GasLimit);

            var persistentState = new PersistentState(this.stateTrack, contractAddress);
            this.stateTrack.CurrentTx = this.smartContractCarrier;

            ReflectionVirtualMachine vm = new ReflectionVirtualMachine(persistentState);
            SmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode,
                decompilation.ContractType.Name,
                this.smartContractCarrier.MethodName,
                new SmartContractExecutionContext
                (
                      new Block(Convert.ToUInt64(this.blockNum), this.coinbaseAddress, Convert.ToUInt64(this.difficulty)),
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

            if (result.Revert)
            {
                this.stateTrack.Rollback();
                return result;
            }

            // We need to append a condensing transaction to the block here if funds are moved.
            IList<TransferInfo> transfers = this.stateTrack.Transfers;
            if (transfers.Any() || this.smartContractCarrier.TxOutValue > 0)
            {
                CondensingTx condensingTx = new CondensingTx(this.smartContractCarrier, transfers, this.stateTrack);
                result.InternalTransactions.Add(condensingTx.CreateCondensingTransaction());
            }
            this.stateTrack.Transfers.Clear();
            this.stateTrack.Commit();

            return result;
        }
    }
}