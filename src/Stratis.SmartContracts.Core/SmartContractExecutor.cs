using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Compilation;
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
        protected readonly IGasMeter gasMeter;
        protected readonly Network network;
        protected readonly IContractStateRepository stateSnapshot;
        protected readonly SmartContractValidator validator;
        protected readonly IKeyEncodingStrategy keyEncodingStrategy;

        protected ulong blockHeight;
        protected uint160 coinbaseAddress;
        protected Money mempoolFee;

        internal ISmartContractExecutionResult Result { get; set; }

        protected SmartContractExecutor(SmartContractCarrier carrier,
            Network network,
            IContractStateRepository stateSnapshot,
            SmartContractValidator validator,
            IKeyEncodingStrategy keyEncodingStrategy,
            Money mempoolFee)
        {
            this.carrier = carrier;
            this.network = network;
            this.stateSnapshot = stateSnapshot.StartTracking();
            this.validator = validator;
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.mempoolFee = mempoolFee;
            this.gasMeter = new GasMeter(this.carrier.GasLimit);
        }

        /// <summary>
        /// Returns the correct SmartContractExecutor based on the carrier opcode.
        /// </summary>
        public static SmartContractExecutor Initialize(SmartContractCarrier carrier,
            Network network,
            IContractStateRepository stateRepository,
            SmartContractValidator validator,
            IKeyEncodingStrategy keyEncodingStrategy,
            Money mempoolFee)
        {
            if (carrier.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                return new CreateSmartContract(carrier, network, stateRepository, validator, keyEncodingStrategy, mempoolFee);
            else
                return new CallSmartContract(carrier, network, stateRepository, validator, keyEncodingStrategy, mempoolFee);
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

        protected ISmartContractExecutionResult CreateContextAndExecute(uint160 contractAddress, byte[] contractCode, string methodName)
        {
            ulong getBalance() => this.stateSnapshot.GetCurrentBalance(contractAddress) + this.carrier.TxOutValue;

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

            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.stateSnapshot, this.gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy, contractAddress, this.network);

            var vm = new ReflectionVirtualMachine(persistentState);
            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode,
                methodName,
                executionContext,
                this.gasMeter,
                new InternalTransactionExecutor(this.stateSnapshot, this.network, this.keyEncodingStrategy),
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
        public CreateSmartContract(SmartContractCarrier carrier,
            Network network,
            IContractStateRepository stateRepository,
            SmartContractValidator validator,
            IKeyEncodingStrategy keyEncodingStrategy,
            Money mempoolFee)
            : base(carrier, network, stateRepository, validator, keyEncodingStrategy, mempoolFee)
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
            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(this.carrier.ContractExecutionCode);
            SmartContractValidationResult validation = this.validator.ValidateContract(decompilation);

            // If validation failed, refund the sender any remaining gas.
            if (!validation.IsValid)
            {
                this.Result = SmartContractExecutionResult.ValidationFailed(this.carrier, validation);
                return;
            }

            // Amount/TxOutValue should always be zero when creating a contract, so we do not need to add it to GetBalance
            ulong getBalance() => this.stateSnapshot.GetCurrentBalance(newContractAddress);

            var block = new Block(this.blockHeight, this.coinbaseAddress.ToAddress(this.network));
            var executionContext = new SmartContractExecutionContext
            (
                block,
                new Message(
                    newContractAddress.ToAddress(this.network),
                    this.carrier.Sender.ToAddress(this.network),
                    this.carrier.TxOutValue,
                    this.carrier.GasLimit
                ),
                this.carrier.GasPrice,
                this.carrier.MethodParameters
            );

            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.stateSnapshot, this.gasMeter, new BasicKeyEncodingStrategy());
            var persistentState = new PersistentState(persistenceStrategy, newContractAddress, this.network);

            var vm = new ReflectionVirtualMachine(persistentState);

            this.Result = vm.Create(
                this.carrier.ContractExecutionCode,
                executionContext,
                this.gasMeter,
                new InternalTransactionExecutor(this.stateSnapshot, this.network, new BasicKeyEncodingStrategy()),
                getBalance);

            if (!this.Result.Revert)
            {
                this.Result.NewContractAddress = newContractAddress;

                // To start with, no value transfers on create. Can call other contracts but send 0 only.
                this.stateSnapshot.SetCode(newContractAddress, this.carrier.ContractExecutionCode);
                this.stateSnapshot.Commit();
            }
        }
    }

    public sealed class CallSmartContract : SmartContractExecutor
    {
        public CallSmartContract(SmartContractCarrier carrier,
            Network network,
            IContractStateRepository stateRepository,
            SmartContractValidator validator,
            IKeyEncodingStrategy keyEncodingStrategy,
            Money mempoolFee)
            : base(carrier, network, stateRepository, validator, keyEncodingStrategy, mempoolFee)
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
          
            // Execute the call to the contract.
            this.Result = base.CreateContextAndExecute(this.carrier.ContractAddress, contractExecutionCode, this.carrier.MethodName);

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