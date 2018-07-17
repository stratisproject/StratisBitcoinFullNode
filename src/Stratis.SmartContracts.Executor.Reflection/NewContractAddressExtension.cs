using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public static class NewContractAddressExtension
    {
        /// <summary>
        /// Get the address for a newly deployed contract.
        /// </summary>
        /// <param name="transaction"></param>
        public static uint160 GetNewContractAddress(this SmartContractCarrier carrier)
        {
            return Core.NewContractAddressExtension.GetContractAddressFromTransactionHash(carrier.TransactionHash);
        }
    }
}
