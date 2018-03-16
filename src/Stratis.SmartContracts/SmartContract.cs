using System;

namespace Stratis.SmartContracts
{
    public abstract class SmartContract
    {
        protected Address Address => this.Message.ContractAddress;

        public ulong Balance => this.getBalance();

        public Gas GasUsed { get; private set; }

        public Block Block { get; }

        public Message Message { get; }

        public IPersistentState PersistentState { get; }
        
        /// <summary>
        /// Used to track the gas usage for this contract instance
        /// </summary>
        private readonly IGasMeter gasMeter;
        private readonly IInternalTransactionExecutor internalTransactionExecutor;
        private readonly Func<ulong> getBalance;
        private readonly ISmartContractState state;

        protected SmartContract(ISmartContractState state)
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            this.Message = state.Message;
            this.Block = state.Block;
            this.PersistentState = state.PersistentState;
            this.gasMeter = state.GasMeter;
            this.internalTransactionExecutor = state.InternalTransactionExecutor;
            this.getBalance = state.GetBalance;
            this.state = state;
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
        protected ITransferResult Transfer(Address addressTo, ulong amount, TransactionDetails transactionDetails = null)
        {
            return this.internalTransactionExecutor.Transfer(this.state, addressTo, amount, transactionDetails);            
        }
    }
}