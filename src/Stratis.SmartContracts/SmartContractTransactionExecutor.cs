using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using System.Text;
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
        private readonly SmartContractTransaction scTransaction;
        private readonly ulong blockNum;
        private readonly ulong difficulty;
        private readonly uint160 coinbaseAddress;

        public SmartContractTransactionExecutor(IContractStateRepository state, 
            SmartContractDecompiler smartContractDecompiler,
            SmartContractValidator smartContractValidator,
            SmartContractGasInjector smartContractGasInjector,
            SmartContractTransaction scTransaction,
            ulong blockNum,
            ulong difficulty,
            uint160 coinbaseAddress)
        {
            this.state = state;
            this.stateTrack = state.StartTracking();
            this.decompiler = smartContractDecompiler;
            this.validator = smartContractValidator;
            this.gasInjector = smartContractGasInjector;
            this.scTransaction = scTransaction;
            this.blockNum = blockNum;
            this.difficulty = difficulty;
            this.coinbaseAddress = coinbaseAddress;
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
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(this.scTransaction.ContractCode);
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
                CallerAddress = this.scTransaction.Sender,
                CallValue = this.scTransaction.Value,
                GasLimit = this.scTransaction.GasLimit,
                GasPrice = this.scTransaction.GasPrice,
                Parameters = this.scTransaction.Parameters ?? new object[0],
                CoinbaseAddress = this.coinbaseAddress,
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

            // To start with, no value transfers on create.

            //IList<TransferInfo> transfers = this.stateTrack.GetTransfers();
            //if (transfers.Any())
            //{
            //    CondensingTx condensingTx = new CondensingTx(transfers, this.scTransaction, this.state);
            //    result.InternalTransactions.Add(condensingTx.CreateCondensingTx());
            //}

            this.stateTrack.SetCode(contractAddress, adjustedCodeBytes);
            this.stateTrack.Commit();
            return result;
        }

        private SmartContractExecutionResult ExecuteCall()
        {
            byte[] contractCode = this.state.GetCode(this.scTransaction.To);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(contractCode); // This is overkill here. Just for testing atm.

            ReflectionVirtualMachine vm = new ReflectionVirtualMachine(this.stateTrack);
            SmartContractExecutionResult result = vm.ExecuteMethod(contractCode, new SmartContractExecutionContext
            {
                BlockNumber = Convert.ToUInt64(this.blockNum),
                Difficulty = Convert.ToUInt64(this.difficulty),
                CallerAddress = this.scTransaction.Sender,
                CallValue = this.scTransaction.Value,
                GasLimit = this.scTransaction.GasLimit,
                GasPrice = this.scTransaction.GasPrice,
                Parameters = this.scTransaction.Parameters ?? new object[0],
                CoinbaseAddress = this.coinbaseAddress,
                ContractAddress = this.scTransaction.To,
                ContractMethod = this.scTransaction.MethodName,
                ContractTypeName = decomp.ContractType.Name
            });

            IList<TransferInfo> transfers = this.stateTrack.GetTransfers();
            if (transfers.Any())
            {
                // need to get the current vin for contract and the tx being executed
                List<StoredVin> vins = new List<StoredVin>();
                StoredVin existingVin = this.state.GetVin(this.scTransaction.To);
                if (existingVin != null)
                    vins.Add(existingVin);
                if (this.scTransaction.Value > 0)
                {
                    vins.Add(new StoredVin
                    {
                        Hash = this.scTransaction.Hash,
                        Nvout = this.scTransaction.Nvout,
                        Value = this.scTransaction.Value
                    });
                }
                

                CondensingTx condensingTx = new CondensingTx(this.scTransaction, transfers, vins, this.stateTrack);
                result.InternalTransactions.Add(condensingTx.CreateCondensingTransaction());
            }

            this.stateTrack.Commit();

            return result;
        }
    }
}
