namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public enum ContractTransactionItemType
    {
        Received,
        Send,
        Staked,
        ContractCall,
        ContractCreate,
        GasRefund
    }
}
