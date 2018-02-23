using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.Exceptions;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts
{
    public class SmartContract
    {
        protected Address Address => this.Message.ContractAddress;

        protected ulong Balance
        {
            get
            {
                return this.stateRepository.GetCurrentBalance(this.Address.ToUint160());
            }
        }

        public Gas GasUsed { get; private set; }

        public Block Block { get; }

        public Message Message { get; }

        public PersistentState PersistentState { get; }

        /// <summary>
        /// Used when creating new contracts or sending funds.
        /// </summary>
        private readonly IContractStateRepository stateRepository;

        /// <summary>
        /// Used to track the gas usage for this contract instance
        /// </summary>
        private readonly GasMeter gasMeter;

        public SmartContract(SmartContractState state)
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            this.Message = state.Message;
            this.Block = state.Block;
            this.PersistentState = state.PersistentState;
            this.stateRepository = state.StateRepository;
            this.gasMeter = state.GasMeter;
            ContractUnspentOutput existingUtxo = state.StateRepository.GetUnspent(this.Address.ToUint160());
            ulong balanceBeforeCall = existingUtxo != null ? existingUtxo.Value : 0;
            //this.Balance = balanceBeforeCall + this.Message.Value;
        }

        /// <summary>
        /// Expends the given amount of gas. If this takes the spent gas over the entered limit, throw an OutOfGasException
        /// </summary>
        /// <param name="spend">TODO: This is currently a ulong instead of a Gas because it needs to receive values from injected IL and it's difficult to create non-primitive types</param>
        public void SpendGas(ulong spend)
        {
            this.gasMeter.Spend((Gas) spend);
        }

        /// <summary>
        /// Sends funds to an address. If the address is a contract and parameters are given, it will execute a method on the contract with the given parameters.
        /// </summary>
        /// <param name="addressTo"></param>
        /// <param name="amount"></param>
        protected TransferResult Transfer(Address addressTo, ulong amount, TransactionDetails transactionDetails = null)
        {
            //TODO: The act of calling this should cost a lot of gas!

            if (this.Balance < amount)
                throw new InsufficientBalanceException();

            // Discern whether is a contract or ordinary address.
            byte[] contractCode = this.stateRepository.GetCode(addressTo.ToUint160());

            if (contractCode == null || contractCode.Length == 0)
            {
                // Is not a contract, so just record the transfer and return
                this.stateRepository.TransferBalance(this.Address.ToUint160(), addressTo.ToUint160(), amount);
                return new TransferResult();
            }

            // It's a contract - instantiate the contract and execute.
            IContractStateRepository track = this.stateRepository.StartTracking();
            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(track, this.gasMeter);
            PersistentState newPersistentState = new PersistentState(track, persistenceStrategy, addressTo.ToUint160());
            Message newMessage = new Message(addressTo, this.Address, amount, (Gas) (this.Message.GasLimit - this.GasUsed));
            SmartContractExecutionContext newContext = new SmartContractExecutionContext(this.Block, newMessage, 0, transactionDetails.Parameters);
            ReflectionVirtualMachine vm = new ReflectionVirtualMachine(newPersistentState);
            SmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode, 
                transactionDetails.ContractTypeName, 
                transactionDetails.ContractMethodName, 
                newContext,
                this.gasMeter);

            SpendGas(result.GasUnitsUsed);

            if (result.Revert)
            {
                // contract execution unsuccessful
                track.Rollback();
                return new TransferResult(null, result.Exception);
            }

            track.Commit();
            this.stateRepository.TransferBalance(this.Address.ToUint160(), addressTo.ToUint160(), amount);
            return new TransferResult(result.Return, null);
        }
    }
}