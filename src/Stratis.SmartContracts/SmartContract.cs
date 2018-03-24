using System;
using System.Globalization;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// All smart contracts created on the Stratis blockchain is required to implement this base class.
    /// <para>
    /// Provides base functionality to spend gas and transfer funds between contracts.
    /// </para>
    /// </summary>
    public abstract class SmartContract
    {
        /// <summary>
        /// The address of the smart contract.
        /// </summary>
        protected Address Address { get { return this.Message.ContractAddress; } }

        /// <summary>
        /// Returns the balance of the smart contract.
        /// </summary>
        public ulong Balance { get { return this.getBalance(); } }

        /// <summary>
        /// TODO: Add documentation
        /// </summary>
        protected readonly Block Block;

        /// <summary>
        /// TODO: Add documentation
        /// </summary>
        protected readonly Message Message;

        /// <summary>
        /// TODO: Add documentation
        /// </summary>
        protected readonly IPersistentState PersistentState;

        /// <summary>
        /// Tracks the gas usage for this contract instance.
        /// </summary>
        private readonly IGasMeter gasMeter;

        /// <summary>
        /// Gets the balance of the contract, if applicable.
        /// </summary>
        private readonly Func<ulong> getBalance;

        /// <summary>
        /// Executes the smart contract.
        /// </summary>
        private readonly IInternalTransactionExecutor internalTransactionExecutor;


        /// <summary>
        /// Provides access to internal hashing functions.
        /// </summary>
        private readonly IInternalHashHelper internalHashHelper;

        /// <summary>
        /// TODO: Add documentation
        /// </summary>
        private readonly ISmartContractState smartContractState;

        protected SmartContract(ISmartContractState smartContractState)
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            this.gasMeter = smartContractState.GasMeter;
            this.Block = smartContractState.Block;
            this.getBalance = smartContractState.GetBalance;
            this.internalTransactionExecutor = smartContractState.InternalTransactionExecutor;
            this.internalHashHelper = smartContractState.InternalHashHelper;
            this.Message = smartContractState.Message;
            this.PersistentState = smartContractState.PersistentState;
            this.smartContractState = smartContractState;
        }

        /// <summary>
        /// Expends the given amount of gas.
        /// <para>
        /// If this takes the spent gas over the entered limit, throw an OutOfGasException
        /// </para>
        /// </summary>
        /// <param name="gasToSpend">TODO: This is currently a ulong instead of a Gas because it needs to receive values from injected IL and it's difficult to create non-primitive types</param>
        public void SpendGas(ulong gasToSpend)
        {
            this.gasMeter.Spend((Gas)gasToSpend);
        }

        /// <summary>
        /// Sends funds to an address.
        /// <para>
        /// If the address is a contract and parameters are given, it will execute a method on the contract with the given parameters.
        /// </para>
        /// </summary>
        /// <param name="addressTo">The address to transfer the funds to.</param>
        /// <param name="amountToTransfer">The amount of funds to transfer in satoshi.</param>
        protected ITransferResult TransferFunds(Address addressTo, ulong amountToTransfer, TransferFundsToContract transactionDetails = null)
        {
            return this.internalTransactionExecutor.TransferFunds(this.smartContractState, addressTo, amountToTransfer, transactionDetails);
        }

        /// <summary>
        /// Returns a 32-byte Keccak256 hash of the given bytes.
        /// </summary>
        /// <param name="toHash"></param>
        /// <returns></returns>
        protected byte[] Keccak256(byte[] toHash)
        {
            return this.internalHashHelper.Keccak256(toHash);
        }

        /// If the input condition is not met, contract execution will be halted by throwing an exception.
        /// </summary>
        /// <param name="condition"></param>
        protected void Assert(bool condition)
        {
            if (!condition)
                throw new Exception("Condition inside 'Assert' call was false.");
        }
    }
}