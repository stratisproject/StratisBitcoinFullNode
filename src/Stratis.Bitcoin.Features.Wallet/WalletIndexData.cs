using Stratis.Bitcoin.Utilities;

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
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotNull(account, nameof(account));
            Guard.NotNull(address, nameof(address));
            Guard.NotNull(transactionData, nameof(transactionData));

            this.Wallet = wallet;
            this.Account = account;
            this.Address = address;
            this.TransactionData = transactionData;
        }
    }
}