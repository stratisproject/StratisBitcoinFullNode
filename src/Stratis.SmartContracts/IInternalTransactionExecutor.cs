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
    }
}