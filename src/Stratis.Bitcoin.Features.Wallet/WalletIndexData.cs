namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Allows direct lookup to each level of the wallet hierarchy without having to search
    /// linearly through lists of addresses, transactions etc.
    /// </summary>
    public class WalletIndexData
    {
        public readonly Wallet Wallet;
        public readonly HdAccount Account;
        public readonly HdAddress Address;
        public readonly TransactionData TransactionData;

        public WalletIndexData(Wallet wallet, HdAccount account, HdAddress address, TransactionData transactionData)
        {
            this.Wallet = wallet;
            this.Account = account;
            this.Address = address;
            this.TransactionData = transactionData;
        }
    }
}