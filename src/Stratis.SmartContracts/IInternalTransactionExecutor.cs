namespace Stratis.SmartContracts
{
    /// <summary>
    /// Handles the execution of transactions that happen internally to a smart contract.
    /// <para>
    /// An example could be a transfer of funds to another contract.
    /// </para>
    /// </summary>
    public interface IInternalTransactionExecutor
    {
        /// <summary>
        /// Transfer funds from one contract to another.
        /// </summary>
        /// <param name="smartContractState">State repository to track and persist changes to the contract.</param>
        /// <param name="addressTo">Where the funds will be transferred to.</param>
        /// <param name="amountToTransfer">The amount to send in satoshi.</param>
        /// <param name="transferFundsToContractDetails">If the address to where the funds will be tranferred to is a contract, supply the details (method name etc).</param>
        ITransferResult TransferFunds(ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer, TransferFundsToContract transferFundsToContractDetails);

        /// <summary>
        /// Create a new contract.
        /// </summary>
        /// <typeparam name="T">Type of contract to create.</typeparam>
        /// <param name="smartContractState">State repository to track and persist changes to the contract.</param>
        /// <param name="parameters">Parameters to be sent to the constructor.</param>
        /// <param name="amountToTransfer">Amount to send in stratoshi.</param>
        ICreateResult Create<T>(ISmartContractState smartContractState, object[] parameters, ulong amountToTransfer);
    }
}