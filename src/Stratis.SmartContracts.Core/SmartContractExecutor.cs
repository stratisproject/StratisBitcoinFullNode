using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Executes the smart contract code, be it a OP_CREATECONTRACT or OP_CALLCONTRACT
    /// </summary>
    public abstract class SmartContractExecutor
    {
        protected readonly SmartContractCarrier carrier;
        protected readonly SmartContractDecompiler decompiler;
        protected readonly ISmartContractGasInjector gasInjector;
        protected readonly IGasMeter gasMeter;
        protected readonly Network network;
        protected readonly IContractStateRepository stateSnapshot;
        protected readonly SmartContractValidator validator;

        protected ulong blockHeight;
        protected uint160 coinbaseAddress;
        protected Money mempoolFee;

        internal ISmartContractExecutionResult Result { get; set; }

        protected SmartContractExecutor(
            SmartContractCarrier carrier,
            SmartContractDecompiler decompiler,
            ISmartContractGasInjector gasInjector,
            Network network,
            IContractStateRepository stateSnapshot,
            SmartContractValidator validator)
        {
            this.carrier = carrier;
            this.decompiler = decompiler;
            this.gasInjector = gasInjector;
            this.network = network;
            this.stateSnapshot = stateSnapshot.StartTracking();
            this.validator = validator;

            this.gasMeter = new GasMeter(this.carrier.GasLimit);
        }

        /// <summary>
        /// Returns a new SmartContractExecutor.
        /// </summary>
        public static SmartContractExecutor Initialize(
            SmartContractCarrier carrier,
            SmartContractDecompiler decompiler,
            ISmartContractGasInjector gasInjector,
            Network network,
            IContractStateRepository stateRepository,
            SmartContractValidator validator)
        {
            if (carrier.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                return new CreateSmartContract(carrier, decompiler, gasInjector, network, stateRepository, validator);
            else
                return new CallSmartContract(carrier, decompiler, gasInjector, network, stateRepository, validator);
        }

        /// <summary>
        /// Returns a new SmartContractExecutor and assigns it the given mempoolFee.
        /// </summary>
        public static SmartContractExecutor Initialize(
            SmartContractCarrier carrier,
            SmartContractDecompiler decompiler,
            ISmartContractGasInjector gasInjector,
            Network network,
            IContractStateRepository stateRepository,
            SmartContractValidator validator,
            Money mempoolFee)
        {
            SmartContractExecutor ret = Initialize(carrier, decompiler, gasInjector, network, stateRepository, validator);
            ret.mempoolFee = mempoolFee;
            return ret;
        }

        public ISmartContractExecutionResult Execute(ulong blockHeight, uint160 coinbaseAddress)
        {
            this.blockHeight = blockHeight;
            this.coinbaseAddress = coinbaseAddress;

            PreExecute();
            OnExecute();
            PostExecute();

            return this.Result;
        }

        protected byte[] AddGasToContractExecutionCode(SmartContractDecompilation decompilation)
        {
            byte[] stream = null;
            using (var memoryStream = new MemoryStream())
            {
                this.gasInjector.AddGasCalculationToContract(decompilation.ContractType, decompilation.BaseType);
                decompilation.ModuleDefinition.Write(memoryStream);
                stream = memoryStream.ToArray();
            }
            return stream;
        }

        protected ISmartContractExecutionResult CreateContextAndExecute(uint160 contractAddress, SmartContractDecompilation decompilation, string methodName)
        {
            ulong getBalance() => this.stateSnapshot.GetCurrentBalance(contractAddress);

            var block = new Block(this.blockHeight, this.coinbaseAddress.ToAddress(this.network));
            var executionContext = new SmartContractExecutionContext
                (
                block,
                new Message(
                    contractAddress.ToAddress(this.network),
                    this.carrier.Sender.ToAddress(this.network),
                    this.carrier.TxOutValue,
                    this.carrier.GasLimit
                    ),
                this.carrier.GasPrice,
                this.carrier.MethodParameters
                );

            byte[] contractCodeWithGas = AddGasToContractExecutionCode(decompilation);
            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.stateSnapshot, this.gasMeter);
            var persistentState = new PersistentState(this.stateSnapshot, persistenceStrategy, contractAddress, this.network);

            var vm = new ReflectionVirtualMachine(persistentState);
            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractCodeWithGas,
                decompilation.ContractType.Name,
                methodName,
                executionContext,
                this.gasMeter,
                new InternalTransactionExecutor(this.stateSnapshot, this.network),
                getBalance);

            return result;
        }

        /// <summary>
        /// Any logic that should happen before we start contract execution and/or validation
        /// happens here.
        /// <para>
        /// A base fee should be spent before contract execution starts.
        /// </para>
        /// </summary>
        private void PreExecute()
        {
            this.gasMeter.Spend(GasPriceList.BaseCost);
        }

        public abstract void OnExecute();

        /// <summary>
        /// Any logic that should happen after contract execution has taken place happens here.
        /// </summary>
        private void PostExecute()
        {
            if (this.mempoolFee != null)
                new SmartContractExecutorResultProcessor(this.Result).Process(this.carrier, this.mempoolFee);
        }
    }

    public sealed class CreateSmartContract : SmartContractExecutor
    {
        public CreateSmartContract(SmartContractCarrier carrier, SmartContractDecompiler decompiler, ISmartContractGasInjector gasInjector, Network network, IContractStateRepository stateRepository, SmartContractValidator validator)
            : base(carrier, decompiler, gasInjector, network, stateRepository, validator)
        {
            Guard.Assert(carrier.OpCodeType == OpcodeType.OP_CREATECONTRACT);
        }

        public override void OnExecute()
        {
            // Create a new address for the contract.
            uint160 newContractAddress = this.carrier.GetNewContractAddress();

            // Create an account for the contract in the state repository.
            this.stateSnapshot.CreateAccount(newContractAddress);

            // Decompile the contract execution code and validate it.
            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(this.carrier.ContractExecutionCode);
            SmartContractValidationResult validation = this.validator.ValidateContract(decompilation);

            // If validation failed, refund the sender any remaining gas.
            if (!validation.IsValid)
            {
                this.Result = SmartContractExecutionResult.ValidationFailed(this.carrier, validation);
                return;
            }

            // Create the smart contract
            MethodDefinition initMethod = decompilation.ContractType.Methods.FirstOrDefault(x => x.CustomAttributes.Any(y => y.AttributeType.FullName == typeof(SmartContractInitAttribute).FullName));
            this.Result = CreateContextAndExecute(newContractAddress, decompilation, initMethod?.Name);

            if (!this.Result.Revert)
            {
                // To start with, no value transfers on create. Can call other contracts but send 0 only.
                this.stateSnapshot.SetCode(newContractAddress, this.carrier.ContractExecutionCode);
                this.stateSnapshot.Commit();
            }
        }
    }

    public sealed class CallSmartContract : SmartContractExecutor
    {
        public CallSmartContract(SmartContractCarrier carrier, SmartContractDecompiler decompiler, ISmartContractGasInjector gasInjector, Network network, IContractStateRepository stateRepository, SmartContractValidator validator)
            : base(carrier, decompiler, gasInjector, network, stateRepository, validator)
        {
            Guard.Assert(carrier.OpCodeType == OpcodeType.OP_CALLCONTRACT);
        }

        public override void OnExecute()
        {
            // Get the contract code (dll) from the repository.
            byte[] contractExecutionCode = this.stateSnapshot.GetCode(this.carrier.ContractAddress);
            if (contractExecutionCode == null)
            {
                this.Result = SmartContractExecutionResult.ContractDoesNotExist(this.carrier);
                return;
            }

            // Decompile the byte code.
            SmartContractDecompilation decompilation = this.decompiler.GetModuleDefinition(contractExecutionCode);

            // YO! VERY IMPORTANT! 
            // Make sure that somewhere around here we check that the method being called ISN'T the SmartContractInit method, or we're in trouble

            // Execute the call to the contract.
            this.stateSnapshot.CurrentCarrier = this.carrier;
            this.Result = base.CreateContextAndExecute(this.carrier.ContractAddress, decompilation, this.carrier.MethodName);

            if (this.Result.Revert)
                this.RevertExecution();
            else
                this.CommitExecution();
        }

        /// <summary>
        /// Contract execution completed successfully, commit state.
        /// <para>
        /// We need to append a condensing transaction to the block if funds are moved.
        /// </para>
        /// </summary>
        private void CommitExecution()
        {
            IList<TransferInfo> transfers = this.stateSnapshot.Transfers;
            if (transfers.Any() || this.carrier.TxOutValue > 0)
            {
                var condensingTx = new CondensingTx(this.carrier, transfers, this.stateSnapshot, this.network);
                this.Result.InternalTransaction = condensingTx.CreateCondensingTransaction();
            }

            this.stateSnapshot.Transfers.Clear();
            this.stateSnapshot.Commit();
        }

        /// <summary>
        /// If funds were sent to the contract and execution failed, we need to send it back to the sender.
        /// </summary>
        private void RevertExecution()
        {
            if (this.carrier.TxOutValue > 0)
            {
                Transaction tx = new CondensingTx(this.carrier, this.network).CreateRefundTransaction();
                this.Result.InternalTransaction = tx;
            }
        }
    }
}