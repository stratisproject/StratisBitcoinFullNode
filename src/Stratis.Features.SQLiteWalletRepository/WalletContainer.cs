using System.Collections.Generic;
using System.Threading;
using ConcurrentCollections;
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
        internal ConcurrentHashSet<string> ParticipatingWallets;
        internal long NextScheduledCatchup;
        internal Dictionary<TopUpTracker, TopUpTracker> Trackers;

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
            this.ParticipatingWallets = new ConcurrentHashSet<string>();

            this.AddressesOfInterest = processBlocksInfo?.AddressesOfInterest ?? new WalletAddressLookup(conn, wallet?.WalletId);
            this.TransactionsOfInterest = processBlocksInfo?.TransactionsOfInterest ?? new WalletTransactionLookup(conn, wallet?.WalletId);
            this.Trackers = processBlocksInfo?.Trackers ?? new Dictionary<TopUpTracker, TopUpTracker>();
        }
    }

    internal class WalletContainer : ProcessBlocksInfo
    {
        internal readonly DBLock LockUpdateWallet;
        internal int ReaderCount;
        public bool HaveWaitingThreads => this.LockUpdateWallet.WaitingThreads > 0;

        internal WalletContainer(DBConnection conn, HDWallet wallet, ProcessBlocksInfo processBlocksInfo = null) : base(conn, processBlocksInfo, wallet)
        {
            this.LockUpdateWallet = new DBLock();
            this.ReaderCount = 0;

            this.Conn = conn;
        }

        internal void WriteLockWait()
        {
            // Only take the write lock if there are no readers.
            while (true)
            {
                this.LockUpdateWallet.Wait();
                if (this.ReaderCount == 0)
                    break;
                this.LockUpdateWallet.Release();
                Thread.Sleep(100);
            }
        }

        internal void WriteLockRelease()
        {
            this.LockUpdateWallet.Release();
        }

        internal void ReadLockWait()
        {
            // Only take a read-lock if there is no writer.
            this.LockUpdateWallet.Wait();
            Interlocked.Increment(ref this.ReaderCount);
            this.LockUpdateWallet.Release();
        }

        internal void ReadLockRelease()
        {
            Interlocked.Decrement(ref this.ReaderCount);
        }
    }
}
