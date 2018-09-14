using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractStateFactory
    {
        /// <summary>
        /// Sets up a new <see cref="ISmartContractState"/> based on the current state.
        /// </summary>        
        ISmartContractState Create(IState state, IGasMeter gasMeter, uint160 address, BaseMessage message,
            IContractState repository);
    }

    public class SmartContractStateFactory : ISmartContractStateFactory
    {
        public SmartContractStateFactory(IContractPrimitiveSerializer serializer,
            Network network,
            IInternalTransactionExecutorFactory internalTransactionExecutorFactory)
        {
            this.Serializer = serializer;
            this.Network = network;
            this.InternalTransactionExecutorFactory = internalTransactionExecutorFactory;
        }

        public Network Network { get; }
        public IContractPrimitiveSerializer Serializer { get; }
        public IInternalTransactionExecutorFactory InternalTransactionExecutorFactory { get; }

        /// <summary>
        /// Sets up a new <see cref="ISmartContractState"/> based on the current state.
        /// </summary>        
        public ISmartContractState Create(IState state, IGasMeter gasMeter, uint160 address, BaseMessage message, IContractState repository)
        {
            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy());

            var persistentState = new PersistentState(persistenceStrategy, new ContractPrimitiveSerializer(this.Network), address);

            var contractState = new SmartContractState(
                state.Block,
                new Message(
                    address.ToAddress(this.Network),
                    message.From.ToAddress(this.Network),
                    message.Amount
                ),
                persistentState,
                this.Serializer,
                gasMeter,
                state.LogHolder,
                this.InternalTransactionExecutorFactory.Create(state),
                new InternalHashHelper(),
                () => state.GetBalance(address));

            return contractState;
        }
    }

    public interface IStateProcessor
    {
        /// <summary>
        /// Applies an externally generated contract creation message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, ExternalCreateMessage message);

        /// <summary>
        /// Applies an internally generated contract creation message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, InternalCreateMessage message);

        /// <summary>
        /// Applies an internally generated contract method call message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, InternalCallMessage message);

        /// <summary>
        /// Applies an externally generated contract method call message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, ExternalCallMessage message);

        /// <summary>
        /// Applies an internally generated contract funds transfer message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, ContractTransferMessage message);
    }

    public class StateProcessor : IStateProcessor
    {
        public StateProcessor(ISmartContractVirtualMachine vm,
            IAddressGenerator addressGenerator)
        {
            this.AddressGenerator = addressGenerator;
            this.Vm = vm;
        }

        public ISmartContractVirtualMachine Vm { get; }

        public IAddressGenerator AddressGenerator { get; }

        private StateTransitionResult ApplyCreate(IState state, object[] parameters, byte[] code, BaseMessage message, string type = null)
        {
            var gasMeter = new GasMeter(message.GasLimit);

            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            uint160 address = state.GenerateAddress(this.AddressGenerator);

            state.ContractState.CreateAccount(address);

            ISmartContractState smartContractState = state.CreateSmartContractState(state, gasMeter, address, message, state.ContractState);

            VmExecutionResult result = this.Vm.Create(state.ContractState, smartContractState, code, parameters, type);

            bool revert = result.ExecutionException != null;

            if (revert)
            {
                return StateTransitionResult.Fail(
                    gasMeter.GasConsumed,
                    result.ExecutionException);
            }

            return StateTransitionResult.Ok(
                gasMeter.GasConsumed,
                address,
                result.Result
            );
        }

        /// <summary>
        /// Applies an externally generated contract creation message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ExternalCreateMessage message)
        {
            return this.ApplyCreate(state, message.Parameters, message.Code, message);
        }

        /// <summary>
        /// Applies an internally generated contract creation message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, InternalCreateMessage message)
        {
            bool enoughBalance = this.EnsureContractHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.InsufficientBalance);

            byte[] contractCode = state.ContractState.GetCode(message.From);

            StateTransitionResult result = this.ApplyCreate(state, message.Parameters, contractCode, message, message.Type);

            // For successful internal creates we need to add the transfer to the internal transfer list.
            // For external creates we do not need to do this.
            if (result.IsSuccess)
            {
                state.AddInternalTransfer(new TransferInfo
                {
                    From = message.From,
                    To = result.Success.ContractAddress,
                    Value = message.Amount
                });
            }

            return result;
        }

        private StateTransitionResult ApplyCall(IState state, CallMessage message, byte[] contractCode)
        {
            var gasMeter = new GasMeter(message.GasLimit);

            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            // This needs to happen after the base fee is charged, which is why it's in here.
            if (message.Method.Name == null)
            {
                return StateTransitionResult.Fail(gasMeter.GasConsumed, StateTransitionErrorKind.NoMethodName);
            }

            string type = state.ContractState.GetContractType(message.To);

            ISmartContractState smartContractState = state.CreateSmartContractState(state, gasMeter, message.To, message, state.ContractState);

            VmExecutionResult result = this.Vm.ExecuteMethod(smartContractState, message.Method, contractCode, type);

            bool revert = result.ExecutionException != null;

            if (revert)
            {
                return StateTransitionResult.Fail(
                    gasMeter.GasConsumed,
                    result.ExecutionException);
            }

            return StateTransitionResult.Ok(
                gasMeter.GasConsumed,
                message.To,
                result.Result
            );
        }

        /// <summary>
        /// Applies an internally generated contract method call message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, InternalCallMessage message)
        {
            bool enoughBalance = this.EnsureContractHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.InsufficientBalance);

            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.NoCode);
            }

            StateTransitionResult result = this.ApplyCall(state, message, contractCode);

            // For successful internal calls we need to add the transfer to the internal transfer list.
            // For external calls we do not need to do this.
            if (result.IsSuccess)
            {
                state.AddInternalTransfer(new TransferInfo
                {
                    From = message.From,
                    To = message.To,
                    Value = message.Amount
                });
            }

            return result;
        }

        /// <summary>
        /// Applies an externally generated contract method call message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ExternalCallMessage message)
        {
            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.NoCode);
            }

            return this.ApplyCall(state, message, contractCode);
        }

        /// <summary>
        /// Applies an internally generated contract funds transfer message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ContractTransferMessage message)
        {
            bool enoughBalance = this.EnsureContractHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.InsufficientBalance);

            // If it's not a contract, create a regular P2PKH tx
            // If it is a contract, do a regular contract call
            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                // No contract at this address, create a regular P2PKH xfer
                state.AddInternalTransfer(new TransferInfo
                {
                    From = message.From,
                    To = message.To,
                    Value = message.Amount
                });

                return StateTransitionResult.Ok((Gas)0, message.To);
            }

            return this.ApplyCall(state, message, contractCode);
        }

        /// <summary>
        /// Checks whether a contract has enough funds to make this transaction.
        /// </summary>
        private bool EnsureContractHasEnoughBalance(IState state, uint160 contractAddress, ulong amountToTransfer)
        {
            ulong balance = state.GetBalance(contractAddress);

            return balance >= amountToTransfer;
        }
    }

    /// <summary>
    /// Represents the current state of the world during a contract execution.
    /// <para>
    /// The state contains several components:
    /// </para>
    /// - The state repository, which contains global account, code, and contract data.
    /// - Internal transfers, which are transfers generated internally by contracts.
    /// - Balance state, which represents the intermediate state of the balances based on the internal transfers list.
    /// - The log holder, which contains logs generated by contracts during execution.
    /// <para>
    /// When a message is applied to the state, the state is updated if the application was successful. Otherwise, the state
    /// is rolled back to a previous snapshot. This works equally for nested state transitions generated by internal creates,
    /// calls and transfers.
    /// </para>
    /// </summary>
    public class State : IState
    {
        private readonly List<TransferInfo> internalTransfers;

        private IState child;
        private readonly ISmartContractStateFactory smartContractStateFactory;

        private State(State state)
        {
            this.ContractState = state.ContractState.StartTracking();
            
            // We create a new log holder but use references to the original raw logs
            this.LogHolder = new ContractLogHolder(state.Network);
            this.LogHolder.AddRawLogs(state.LogHolder.GetRawLogs());

            // We create a new list but use references to the original transfers.
            this.internalTransfers = new List<TransferInfo>(state.InternalTransfers);

            // Create a new balance state based off the old one but with the repository and internal transfers list reference
            this.BalanceState = new BalanceState(this.ContractState, state.BalanceState.TxAmount, this.internalTransfers);
            this.Network = state.Network;
            this.Nonce = state.Nonce;
            this.Block = state.Block;
            this.TransactionHash = state.TransactionHash;
            this.smartContractStateFactory = state.smartContractStateFactory;
        }

        public State(IContractState repository,
            IBlock block,
            Network network,
            ulong txAmount,
            uint256 transactionHash,
            ISmartContractStateFactory smartContractStateFactory)
        {
            this.ContractState = repository;
            this.LogHolder = new ContractLogHolder(network);
            this.internalTransfers = new List<TransferInfo>();
            this.BalanceState = new BalanceState(this.ContractState, txAmount, this.InternalTransfers);
            this.Network = network;
            this.Nonce = 0;
            this.Block = block;
            this.TransactionHash = transactionHash;
            this.smartContractStateFactory = smartContractStateFactory;
        }
        
        public uint256 TransactionHash { get; }

        public IBlock Block { get; }

        private Network Network { get; }

        public ulong Nonce { get; private set; }

        public IContractLogHolder LogHolder { get; }

        public BalanceState BalanceState { get; }

        public IReadOnlyList<TransferInfo> InternalTransfers => this.internalTransfers;

        public ulong GetNonceAndIncrement()
        {
            return this.Nonce++;
        }

        public IContractState ContractState { get; }

        /// <summary>
        /// Sets up a new <see cref="ISmartContractState"/> based on the current state.
        /// </summary>
         public ISmartContractState CreateSmartContractState(IState state, GasMeter gasMeter, uint160 address, BaseMessage message, IContractState repository) 
        {
            return this.smartContractStateFactory.Create(state, gasMeter, address, message, repository);
        }

        /// <summary>
        /// Returns contract logs in the log type used by consensus.
        /// </summary>
        public IList<Log> GetLogs(IContractPrimitiveSerializer serializer)
        {
            return this.LogHolder.GetRawLogs().ToLogs(serializer);
        }       

        public void TransitionTo(IState state)
        {
            if (this.child != state)
            {
                throw new ArgumentException("New state must be a child of this state.");
            }

            // Update internal transfers
            this.internalTransfers.Clear();
            this.internalTransfers.AddRange(state.InternalTransfers);

            // Update logs
            this.LogHolder.Clear();
            this.LogHolder.AddRawLogs(state.LogHolder.GetRawLogs());

            // Update nonce
            this.Nonce = state.Nonce;

            // Commit the state to update the parent state
            state.ContractState.Commit();

            this.child = null;
        }

        public void AddInternalTransfer(TransferInfo transferInfo)
        {
            this.internalTransfers.Add(transferInfo);
        }

        public ulong GetBalance(uint160 address)
        {
            return this.BalanceState.GetBalance(address);
        }

        public uint160 GenerateAddress(IAddressGenerator addressGenerator)
        {
            return addressGenerator.GenerateAddress(this.TransactionHash, this.GetNonceAndIncrement());
        }

        /// <summary>
        /// Returns a mutable snapshot of the current state. Changes can be made to the snapshot, then discarded or applied to the parent state.
        /// To update this state with changes made to the snapshot, call <see cref="TransitionTo"/>. Only one valid snapshot can exist. If a new
        /// snapshot is created, the parent state will reject any transitions from older snapshots.
        /// </summary>
        public IState Snapshot()
        {
            this.child = new State(this);

            return this.child;
        }
    }
}