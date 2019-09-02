using NBitcoin;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// Tracks block processing state for a wallet or multiple wallets associated with a connection.
    /// </summary>
    internal class ProcessBlocksInfo
    {
        internal TempTable Outputs;
        internal TempTable PrevOuts;
        internal AddressesOfInterest AddressesOfInterest;
        internal TransactionsOfInterest TransactionsOfInterest;
        internal ChainedHeader NewTip;
        internal ChainedHeader PrevTip;
        internal bool MustCommit;
        internal DBConnection Conn;
        internal HDWallet Wallet;
        internal long NextScheduledCatchup;

        internal DBLock LockProcessBlocks;

        internal ProcessBlocksInfo(DBConnection conn, ProcessBlocksInfo processBlocksInfo, HDWallet wallet = null)
        {
            this.NewTip = null;
            this.PrevTip = null;
            this.MustCommit = false;
            this.Conn = conn;
            this.Wallet = wallet;
            this.LockProcessBlocks = processBlocksInfo?.LockProcessBlocks ?? new DBLock();
            this.Outputs = TempTable.Create<TempOutput>();
            this.PrevOuts = TempTable.Create<TempPrevOut>();

            this.AddressesOfInterest = processBlocksInfo?.AddressesOfInterest ?? new AddressesOfInterest(conn, wallet?.WalletId);
            this.TransactionsOfInterest = processBlocksInfo?.TransactionsOfInterest ?? new TransactionsOfInterest(conn, wallet?.WalletId);
        }
    }

    internal class WalletContainer : ProcessBlocksInfo
    {
        internal readonly DBLock LockUpdateWallet;
        internal readonly DBLock LockUpdateAccounts;
        internal readonly DBLock LockUpdateAddresses;

        internal WalletContainer(DBConnection conn, HDWallet wallet, ProcessBlocksInfo processBlocksInfo = null) : base(conn, processBlocksInfo, wallet)
        {
            this.LockUpdateWallet = new DBLock();
            this.LockUpdateAccounts = new DBLock();
            this.LockUpdateAddresses = new DBLock();
            this.Conn = conn;
        }
    }
}
