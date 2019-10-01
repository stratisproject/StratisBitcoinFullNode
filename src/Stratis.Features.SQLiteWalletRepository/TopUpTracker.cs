using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Features.SQLiteWalletRepository.External;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// This class tracks an address type to top-up while scanning block transactions.
    /// </summary>
    internal class TopUpTracker : ITopUpTracker
    {
        public int WalletId {get; private set; }
        public int AccountIndex { get; private set; }
        public int AddressType { get; private set; }
        public int AddressCount { get; internal set; }
        public int NextAddressIndex { get; internal set; }
        public bool IsWatchOnlyAccount { get; internal set; }

        private DBConnection conn;
        private ProcessBlocksInfo processBlocksInfo;

        internal HDAccount Account;

        internal TopUpTracker(DBConnection conn, int walletId, int accountIndex, int addressType)
        {
            this.conn = conn;
            this.processBlocksInfo = null;

            this.WalletId = walletId;
            this.AccountIndex = accountIndex;
            this.AddressType = addressType;
        }

        internal TopUpTracker(ProcessBlocksInfo processBlocksInfo, int walletId, int accountIndex, int addressType) :
            this(processBlocksInfo.Conn, walletId, accountIndex, addressType)
        {
            this.processBlocksInfo = processBlocksInfo;
        }

        internal void ReadAccount()
        {
            this.Account = HDAccount.GetAccount(this.conn, this.WalletId, this.AccountIndex);
            this.AddressCount = HDAddress.GetAddressCount(this.conn, this.WalletId, this.AccountIndex, this.AddressType);
            this.NextAddressIndex = HDAddress.GetNextAddressIndex(this.conn, this.WalletId, this.AccountIndex, this.AddressType);
            this.IsWatchOnlyAccount = this.Account.ExtPubKey == null;
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

        public AddressIdentifier CreateAddress()
        {
            HDAddress newAddress = this.conn.Repository.CreateAddress(this.Account, this.AddressType, this.AddressCount);

            if (!this.conn.IsInTransaction)
            {
                // We've postponed creating a transaction since we weren't sure we will need it.
                // Create it now.
                // TODO: Perhaps just add it to the tentative collection.
                this.conn.BeginTransaction();
                if (this.processBlocksInfo != null)
                    this.processBlocksInfo.MustCommit = true;
            }

            // Insert the new address into the database.
            this.conn.Insert(newAddress);

            // Update the information in the tracker.
            this.NextAddressIndex++;
            this.AddressCount++;

            return new AddressIdentifier()
            {
                WalletId = newAddress.WalletId,
                AccountIndex = newAddress.AccountIndex,
                AddressType = newAddress.AddressType,
                AddressIndex = newAddress.AddressIndex,
                ScriptPubKey = newAddress.ScriptPubKey
            };
        }
    }
}
