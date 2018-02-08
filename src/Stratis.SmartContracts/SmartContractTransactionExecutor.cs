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
        private readonly SmartContractCarrier scTransaction;
        private readonly ulong blockNum;
        private readonly ulong difficulty;

        public SmartContractTransactionExecutor(IContractStateRepository state,
            SmartContractDecompiler smartContractDecompiler,
            SmartContractValidator smartContractValidator,
            SmartContractGasInjector smartContractGasInjector,
            SmartContractCarrier scTransaction,
            ulong blockNum,
            ulong difficulty)
        {
            this.state = state;
            this.stateTrack = state.StartTracking();
            this.decompiler = smartContractDecompiler;
            this.validator = smartContractValidator;
            this.gasInjector = smartContractGasInjector;
            this.scTransaction = scTransaction;
            this.blockNum = blockNum;
            this.difficulty = difficulty;
        }

        public SmartContractExecutionResult Execute()
        {
            // ASSERT OPCODETYPE == CREATE || CALL
            return (this.scTransaction.OpCodeType == OpcodeType.OP_CREATECONTRACT) ? ExecuteCreate() : ExecuteCall();
        }

        private SmartContractExecutionResult ExecuteCreate()
        {
            uint160 contractAddress = this.scTransaction.GetNewContractAddress(); // TODO: GET ACTUAL NUM
            this.state.CreateAccount(0);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(this.scTransaction.ContractExecutionCode);
            SmartContractValidationResult validationResult = this.validator.ValidateContract(decomp);

            if (!validationResult.Valid)
            {
                // expend all of users fee - no deployment
                throw new NotImplementedException();
            }

            this.gasInjector.AddGasCalculationToContract(decomp.ContractType, decomp.BaseType);
            MemoryStream adjustedCodeMem = new MemoryStream();
            decomp.ModuleDefinition.Write(adjustedCodeMem);
            byte[] adjustedCodeBytes = adjustedCodeMem.ToArray();
            ReflectionVirtualMachine vm = new ReflectionVirtualMachine(this.stateTrack);

            MethodDefinition initMethod = decomp.ContractType.Methods.FirstOrDefault(x => x.CustomAttributes.Any(y => y.AttributeType.FullName == typeof(SmartContractInitAttribute).FullName));

            SmartContractExecutionResult result = vm.ExecuteMethod(adjustedCodeMem.ToArray(), new SmartContractExecutionContext
            {
                BlockNumber = blockNum,
                Difficulty = difficulty,
                CallerAddress = 100, // TODO: FIX THIS
                CallValue = this.scTransaction.TxOutValue,
                GasLimit = this.scTransaction.GasLimit,
                GasPrice = this.scTransaction.GasPrice,
                Parameters = this.scTransaction.MethodParameters ?? new object[0],
                CoinbaseAddress = 0, //TODO: FIX THIS
                ContractAddress = contractAddress,
                ContractMethod = initMethod?.Name, // probably better ways of doing this
                ContractTypeName = decomp.ContractType.Name // probably better ways of doing this
            });
            // do something with gas

            if (result.Revert)
            {
                this.stateTrack.Rollback();
                return result;
            }

            IList<TransferInfo> transfers = this.state.GetTransfers();
            if (transfers.Any())
            {
                CondensingTx condensingTx = new CondensingTx(transfers, this.scTransaction);
                result.InternalTransactions.Add(condensingTx.CreateCondensingTx());
            }

            this.stateTrack.SetCode(contractAddress, adjustedCodeBytes);
            this.stateTrack.Commit();
            return result;
        }

        private SmartContractExecutionResult ExecuteCall()
        {
            byte[] contractCode = this.state.GetCode(this.scTransaction.To);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(contractCode); // This is overkill here. Just for testing atm.

            ReflectionVirtualMachine vm = new ReflectionVirtualMachine(this.state);
            SmartContractExecutionResult result = vm.ExecuteMethod(contractCode, new SmartContractExecutionContext
            {
                BlockNumber = Convert.ToUInt64(this.blockNum),
                Difficulty = Convert.ToUInt64(this.difficulty),
                CallerAddress = 0, // TODO: FIX THIS
                CallValue = this.scTransaction.TxOutValue,
                GasLimit = this.scTransaction.GasLimit,
                GasPrice = this.scTransaction.GasPrice,
                Parameters = this.scTransaction.MethodParameters ?? new object[0],
                CoinbaseAddress = 0, //TODO: FIX THIS
                ContractAddress = this.scTransaction.To,
                ContractMethod = this.scTransaction.MethodName,
                ContractTypeName = decomp.ContractType.Name
            });

            IList<TransferInfo> transfers = this.state.GetTransfers();
            if (transfers.Any())
            {
                CondensingTx condensingTx = new CondensingTx(transfers, this.scTransaction);
                result.InternalTransactions.Add(condensingTx.CreateCondensingTx());
            }

            return result;
        }
    }
}