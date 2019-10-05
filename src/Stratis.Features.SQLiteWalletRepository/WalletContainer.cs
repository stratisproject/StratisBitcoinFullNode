using System.Collections.Generic;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
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
        internal WalletAddressLookup AddressesOfInterest;
        internal WalletTransactionLookup TransactionsOfInterest;
        internal ChainedHeader NewTip;
        internal HashHeightPair PrevTip;
        internal bool MustCommit;
        internal DBConnection Conn;
        internal HDWallet Wallet;
        internal List<string> ParticipatingWallets;
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
            this.ParticipatingWallets = new List<string>();

            this.AddressesOfInterest = processBlocksInfo?.AddressesOfInterest ?? new WalletAddressLookup(conn, wallet?.WalletId);
            this.TransactionsOfInterest = processBlocksInfo?.TransactionsOfInterest ?? new WalletTransactionLookup(conn, wallet?.WalletId);
        }
    }

    internal class WalletContainer : ProcessBlocksInfo
    {
        private readonly DBLock lockUpdateWallet;
        private int readers;
        public bool HaveWaitingThreads => this.lockUpdateWallet.WaitingThreads > 0;

        internal WalletContainer(DBConnection conn, HDWallet wallet, ProcessBlocksInfo processBlocksInfo = null) : base(conn, processBlocksInfo, wallet)
        {
            this.lockUpdateWallet = new DBLock();
            this.readers = 0;

            this.Conn = conn;
        }

        internal void WriteLockWait()
        {
            // Only take the write lock if there are no readers.
            while (true)
            {
                this.lockUpdateWallet.Wait();
                if (this.readers == 0)
                    break;
                this.lockUpdateWallet.Release();
                Thread.Sleep(100);
            }
        }

        internal void WriteLockRelease()
        {
            this.lockUpdateWallet.Release();
        }

        internal void ReadLockWait()
        {
            // Only take a read-lock if there is no writer.
            this.lockUpdateWallet.Wait();
            Interlocked.Increment(ref this.readers);
            this.lockUpdateWallet.Release();
        }

        internal void ReadLockRelease()
        {
            Interlocked.Decrement(ref this.readers);
        }
    }
}
