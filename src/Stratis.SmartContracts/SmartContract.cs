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
        /// Holds details about the current block.
        /// </summary>
        protected readonly IBlock Block;

        /// <summary>
        /// Holds details about the current transaction that has been sent.
        /// </summary>
        protected readonly IMessage Message;

        /// <summary>
        ///  Provides functionality for the saving and retrieval of objects inside smart contracts.
        /// </summary>
        protected readonly IPersistentState PersistentState;

        /// <summary>
        /// Provides functionality for the serialization and deserialization of primitives to bytes inside smart contracts.
        /// </summary>
        protected readonly ISerializer Serializer;

        /// <summary>
        /// Tracks the gas usage for this contract instance.
        /// </summary>
        private readonly IGasMeter gasMeter;

        /// <summary>
        /// Gets the balance of the contract, if applicable.
        /// </summary>
        private readonly Func<ulong> getBalance;

        /// <summary>
        /// Saves any logs during contract execution.
        /// </summary>
        private readonly IContractLogger contractLogger;

        /// <summary>
        /// Executes any internal calls or creates to other smart contracts.
        /// </summary>
        private readonly IInternalTransactionExecutor internalTransactionExecutor;


        /// <summary>
        /// Provides access to internal hashing functions.
        /// </summary>
        private readonly IInternalHashHelper internalHashHelper;

        /// <summary>
        /// The current state of the blockchain and current transaction.
        /// </summary>
        private readonly ISmartContractState smartContractState;

        public SmartContract(ISmartContractState smartContractState)
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            this.gasMeter = smartContractState.GasMeter;
            this.Block = smartContractState.Block;
            this.getBalance = smartContractState.GetBalance;
            this.contractLogger = smartContractState.ContractLogger;
            this.internalTransactionExecutor = smartContractState.InternalTransactionExecutor;
            this.internalHashHelper = smartContractState.InternalHashHelper;
            this.Message = smartContractState.Message;
            this.PersistentState = smartContractState.PersistentState;
            this.Serializer = smartContractState.Serializer;
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
        /// 
        /// If address belongs to a contract, will invoke the receive function on this contract. 
        /// </summary>
        /// <param name="addressTo">The address to transfer the funds to.</param>
        /// <param name="amountToTransfer">The amount of funds to transfer in satoshi.</param>
        protected ITransferResult Transfer(Address addressTo, ulong amountToTransfer)
        {
            return this.internalTransactionExecutor.Transfer(this.smartContractState, addressTo, amountToTransfer);
        }

        /// <summary>
        /// Call a method on another contract.
        /// </summary>
        /// <param name="addressTo">The contract on which to call the method.</param>
        /// <param name="amountToTransfer">The amount of funds to transfer in satoshi.</param>
        /// <param name="methodName">The name of the method to call on the contract.</param>
        /// <param name="parameters">The parameters to inject to the method call.</param>
        /// <param name="gasLimit">The total amount of gas to allow this call to take up. Default is to use all remaining gas.</param>
        protected ITransferResult Call(Address addressTo, ulong amountToTransfer, string methodName, object[] parameters = null, ulong gasLimit = 0)
        {
            return this.internalTransactionExecutor.Call(this.smartContractState, addressTo, amountToTransfer, methodName, parameters, gasLimit);
        }

        /// <summary>
        /// Creates a new contract.
        /// </summary>
        /// <typeparam name="T">Contract type to instantiate.</typeparam>
        /// <param name="amountToTransfer">The amount of funds to transfer in satoshi.</param>
        /// <param name="parameters">The parameters to inject to the constructor.</param>
        /// <param name="gasLimit">The total amount of gas to allow this call to take up. Default is to use all remaining gas.</param>
        protected ICreateResult Create<T>(ulong amountToTransfer = 0, object[] parameters = null, ulong gasLimit = 0) where T : SmartContract
        {
            return this.internalTransactionExecutor.Create<T>(this.smartContractState, amountToTransfer, parameters, gasLimit);
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

        /// <summary>
        /// If the input condition is not met, contract execution will be halted by throwing an exception.
        /// </summary>
        protected void Assert(bool condition, string message = "Assert failed.")
        {
            if (!condition)
                throw new SmartContractAssertException(message);
        }

        /// <summary>
        /// Log an event. Useful for front-end interactions with your contract.
        /// </summary>
        /// <typeparam name="T">Any struct.</typeparam>
        /// <param name="toLog">Object with fields to save in logs.</param>
        protected void Log<T>(T toLog) where T : struct
        {
            this.contractLogger.Log(this.smartContractState, toLog);
        }

        /// The fallback method, invoked when a transaction provides a method name of <see cref="string.Empty"/>.
        /// The fallback method. Override this method to define behaviour when the contract receives funds and the method name in the calling transaction equals <see cref="string.Empty"/>.
        /// Override this method to define behaviour when the contract receives funds and the method name in the calling transaction equals <see cref="string.Empty"/>.
        /// <para>
        /// This occurs when a contract sends funds to another contract using <see cref="Transfer"/>.
        /// </para>
        /// </summary>
        public virtual void Receive() {}
    }
}