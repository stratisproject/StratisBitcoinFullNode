using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// This class tracks an address type to top-up while scanning block transactions.
    /// </summary>
    internal class TopUpTracker
    {
        internal int WalletId;
        internal int AccountIndex;
        internal int AddressType;
        internal HDAccount Account;
        internal int AddressCount;
        internal int NextAddressIndex;

        internal TopUpTracker(int walletId, int accountIndex, int addressType)
        {
            this.WalletId = walletId;
            this.AccountIndex = accountIndex;
            this.AddressType = addressType;
        }

        internal void ReadAccount(DBConnection conn)
        {
            this.Account = HDAccount.GetAccount(conn, this.WalletId, this.AccountIndex);
            this.AddressCount = HDAddress.GetAddressCount(conn, this.WalletId, this.AccountIndex, this.AddressType);
            this.NextAddressIndex = HDAddress.GetNextAddressIndex(conn, this.WalletId, this.AccountIndex, this.AddressType);
        }

        public override int GetHashCode()
        {
            return (this.WalletId << 8) ^ (this.AccountIndex << 4) ^ this.AddressType;
        }

        public override bool Equals(object obj)
        {
            return (obj as TopUpTracker).WalletId == this.WalletId &&
                   (obj as TopUpTracker).AccountIndex == this.AccountIndex &&
                   (obj as TopUpTracker).AddressType == this.AddressType;
        }
    }
}
