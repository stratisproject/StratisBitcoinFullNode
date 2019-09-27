using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using SQLite;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository.Commands;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// This class represents a connection to the repository. Its a central point for all functionality that can be performed via a connection.
    /// </summary>
    public class DBConnection
    {
        internal SQLiteConnection SQLiteConnection;
        public SQLiteWalletRepository Repository;
        public Stack<(dynamic, Action<dynamic>)> RollBackActions;
        public Stack<(dynamic, Action<dynamic>)> CommitActions;
        internal Dictionary<string, DBCommand> Commands;

        // A given connection can't have two transactions running in parallel.
        internal DBLock TransactionLock;
        internal int TransactionDepth;
        internal bool IsInTransaction => this.SQLiteConnection.IsInTransaction;

        internal Dictionary<string, long> Metrics = new Dictionary<string, long>();

        public DBConnection(SQLiteWalletRepository repo, string dbFile)
        {
            string path = Path.Combine(repo.DBPath, dbFile);

            try
            {
                this.SQLiteConnection = new SQLiteConnection(path);
            }
            catch (SQLiteException err) when (err.Result == SQLite3.Result.CannotOpen)
            {
                if (!File.Exists(path) && path.Length > 259)
                    throw new InvalidOperationException($"Your database path of {path.Length} characters may be too long: '{err.Message}'.");

                throw;
            }

            this.Repository = repo;
            this.TransactionLock = new DBLock();// new SemaphoreSlim(1, 1);
            this.TransactionDepth = 0;
            this.CommitActions = new Stack<(object, Action<object>)>();
            this.RollBackActions = new Stack<(object, Action<object>)>();
            this.Commands = new Dictionary<string, DBCommand>();
            this.RegisterProcessBlockCommands();
        }

        internal void AddCommitAction(object commitData, Action<object> commitAction)
        {
            this.CommitActions.Push((commitData, commitAction));
        }

        internal void AddRollbackAction(object rollBackData, Action<object> rollBackAction)
        {
            this.RollBackActions.Push((rollBackData, rollBackAction));
        }

        public static implicit operator SQLiteConnection(DBConnection d) => d.SQLiteConnection;

        internal void BeginTransaction()
        {
            if (!this.IsInTransaction)
            {
                this.TransactionLock.Wait();
                this.SQLiteConnection.BeginTransaction();
                Guard.Assert(this.SQLiteConnection.IsInTransaction);
                this.TransactionDepth = 0;
            }

            this.TransactionDepth++;
        }

        internal void Rollback()
        {
            Guard.Assert(this.SQLiteConnection.IsInTransaction);

            this.TransactionDepth--;

            if (this.TransactionDepth == 0)
            {
                this.SQLiteConnection.Rollback();
                this.CommitActions.Clear();

                while (this.RollBackActions.Count > 0)
                {
                    (dynamic rollBackData, Action<dynamic> rollBackAction) = this.RollBackActions.Pop();

                    rollBackAction(rollBackData);
                }

                this.TransactionLock.Release();
            }
        }

        internal void Commit()
        {
            Guard.Assert(this.SQLiteConnection.IsInTransaction);

            this.TransactionDepth--;

            if (this.TransactionDepth == 0)
            {
                this.SQLiteConnection.Commit();
                this.RollBackActions.Clear();

                while (this.CommitActions.Count > 0)
                {
                    (dynamic commitData, Action<dynamic> commitAction) = this.CommitActions.Pop();

                    commitAction(commitData);
                }

                this.TransactionLock.Release();
            }
        }

        internal DBCommand CreateCommand(string cmdText, params object[] ps)
        {
            return new DBCommand(this, cmdText, ps);
        }

        internal List<T> Query<T>(string query, params object[] args) where T:new()
        {
            return this.SQLiteConnection.Query<T>(query, args);
        }

        internal void Insert(object obj)
        {
            this.SQLiteConnection.Insert(obj);
        }

        internal void Delete<T>(object obj)
        {
            this.SQLiteConnection.Delete<T>(obj);
        }

        internal void InsertOrReplace(object obj)
        {
            this.SQLiteConnection.InsertOrReplace(obj);
        }

        internal T Find<T>(object pk) where T : new()
        {
            return this.SQLiteConnection.Find<T>(pk);
        }

        internal T FindWithQuery<T>(string query, params object[] args) where T : new()
        {
            return this.SQLiteConnection.FindWithQuery<T>(query, args);
        }

        internal void Execute(string query, params object[] args)
        {
            this.SQLiteConnection.Execute(query, args);
        }

        internal T ExecuteScalar<T>(string query, params object[] args) where T : new()
        {
            return this.SQLiteConnection.ExecuteScalar<T>(query, args);
        }

        internal void Close()
        {
            this.SQLiteConnection.Close();
        }

        internal void CreateDBStructure()
        {
            this.CreateTable<HDWallet>();
            this.CreateTable<HDAccount>();
            this.CreateTable<HDAddress>();
            this.CreateTable<HDTransactionData>();
            this.CreateTable<HDPayment>();
        }

        internal void AddAdresses(HDAccount account, int addressType, List<HdAddress> hdAddresses)
        {
            foreach (HdAddress hdAddress in hdAddresses)
            {
                HDAddress address = this.Repository.CreateAddress(account, addressType, hdAddress.Index);
                address.ScriptPubKey = hdAddress.ScriptPubKey?.ToHex();
                address.PubKey = hdAddress.Pubkey?.ToHex();

                this.Insert(address);
            }
        }

        internal List<HDAddress> CreateAddresses(HDAccount account, int addressType, int addressesQuantity)
        {
            var addresses = new List<HDAddress>();

            int addressCount = HDAddress.GetAddressCount(this.SQLiteConnection, account.WalletId, account.AccountIndex, addressType);

            for (int addressIndex = addressCount; addressIndex < (addressCount + addressesQuantity); addressIndex++)
            {
                HDAddress address = this.Repository.CreateAddress(account, addressType, addressIndex);
                this.Insert(address);
                addresses.Add(address);
            }

            return addresses;
        }

        internal HDAddress CreateAddress(HDAccount account, int addressType, int addressIndex)
        {
            // Retrieve the pubkey associated with the private key of this address index.
            var keyPath = new KeyPath($"{addressType}/{addressIndex}");

            Script pubKeyScript = null;
            Script scriptPubKey = null;

            if (account.ExtPubKey != null)
            {
                ExtPubKey extPubKey = account.GetExtPubKey(this.Repository.Network).Derive(keyPath);
                PubKey pubKey = extPubKey.PubKey;
                pubKeyScript = pubKey.ScriptPubKey;
                scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(pubKey);
            }

            // Add the new address details to the list of addresses.
            return new HDAddress()
            {
                WalletId = account.WalletId,
                AccountIndex = account.AccountIndex,
                AddressType = addressType,
                AddressIndex = addressIndex,
                PubKey = pubKeyScript?.ToHex(),
                ScriptPubKey = scriptPubKey?.ToHex()
            };
        }

        internal IEnumerable<HDAddress> GetUsedAddresses(int walletId, int accountIndex, int addressType, int count)
        {
            return HDAddress.GetUsedAddresses(this, walletId, accountIndex, addressType, count);
        }

        internal HDAccount CreateAccount(int walletId, int accountIndex, string accountName, string extPubKey, long creationTimeSeconds)
        {
            var account = new HDAccount()
            {
                WalletId = walletId,
                AccountIndex = accountIndex,
                AccountName = accountName,
                ExtPubKey = extPubKey,
                CreationTime = creationTimeSeconds,
            };

            this.Insert(account);

            return account;
        }

        internal bool TableExists(string tableName)
        {
            return this.ExecuteScalar<int>($@"
                SELECT  COUNT(*)
                FROM    sqlite_master
                WHERE   name = ? and type = 'table';",
                tableName) != 0;
        }

        internal void CreateTable<T>()
        {
            if (!this.TableExists(typeof(T).Name))
            {
                if (typeof(T) == typeof(HDWallet))
                    HDWallet.CreateTable(this);
                else if (typeof(T) == typeof(HDAccount))
                    HDAccount.CreateTable(this);
                else if (typeof(T) == typeof(HDAddress))
                    HDAddress.CreateTable(this);
                else if (typeof(T) == typeof(HDTransactionData))
                    HDTransactionData.CreateTable(this);
                else if (typeof(T) == typeof(HDPayment))
                    HDPayment.CreateTable(this);
            }
        }

        internal HDWallet GetWalletByName(string walletName)
        {
            return HDWallet.GetByName(this, walletName);
        }

        internal HDAccount GetAccountByName(string walletName, string accountName)
        {
            return this.FindWithQuery<HDAccount>($@"
                SELECT  A.*
                FROM    HDAccount A
                JOIN    HDWallet W
                ON      W.Name = ?
                AND     W.WalletId = A.WalletId
                WHERE   A.AccountName = ?", walletName, accountName);
        }

        internal IEnumerable<HDAccount> GetAccounts(int walletId, string accountName = null)
        {
            return HDAccount.GetAccounts(this, walletId, accountName);
        }

        internal HDWallet GetById(int walletId)
        {
            return this.Find<HDWallet>(walletId);
        }

        internal HDAccount GetById(int walletId, int accountIndex)
        {
            return HDAccount.GetAccount(this, walletId, accountIndex);
        }

        internal IEnumerable<HDAddress> GetUnusedAddresses(int walletId, int accountIndex, int addressType, int count)
        {
            return HDAddress.GetUnusedAddresses(this, walletId, accountIndex, addressType, count);
        }

        internal IEnumerable<HDTransactionData> GetSpendableOutputs(int walletId, int accountIndex, int currentChainHeight, long coinbaseMaturity, int confirmations = 0)
        {
            return HDTransactionData.GetSpendableTransactions(this, walletId, accountIndex, currentChainHeight, coinbaseMaturity, confirmations);
        }

        internal IEnumerable<HDTransactionData> GetTransactionsForAddress(int walletId, int accountIndex, int addressType, int addressIndex)
        {
            return HDTransactionData.GetAllTransactions(this, walletId, accountIndex, addressType, addressIndex);
        }

        private IEnumerable<(string txId, long unixTimeSeconds)> RemoveTransactionsByTxToDelete(string outputFilter, string spendFilter)
        {
            var removedTxs = new Dictionary<string, long>();

            this.Execute($@"
            DROP    TABLE IF EXISTS temp.TxToDelete");

            this.Execute($@"
            CREATE  TABLE temp.TxToDelete (
                    WalletId INT
            ,       AccountIndex INT
            ,       AddressType INT
            ,       AddressIndex INT
            ,       OutputTxId TEXT
            ,       OutputIndex INT
            ,       ScriptPubKey TEXT)");

            this.Execute($@"
            INSERT  INTO temp.TxToDelete (
                    WalletId
            ,       AccountIndex
            ,       AddressType
            ,       AddressIndex
            ,       OutputTxId
            ,       OutputIndex
            ,       ScriptPubKey)
            SELECT  WalletId
            ,       AccountIndex
            ,       AddressType
            ,       AddressIndex
            ,       OutputTxId
            ,       OutputIndex
            ,       ScriptPubKey
            FROM    HDTransactionData
            {outputFilter}");

            this.Execute($@"
            DELETE FROM HDPayment
            WHERE  (SpendTxTime, SpendTxId, OutputTxId, OutputIndex, ScriptPubKey) IN (
                    SELECT SpendTxTime
                    ,      SpendTxId
                    ,      OutputTxId
                    ,      OutputIndex
                    ,      ScriptPubKey
                    FROM   HDTransactionData
                    {spendFilter})");


            foreach (HDTransactionData td in this.Query<HDTransactionData>($@"
            SELECT  *
            FROM    HDTransactionData
            WHERE   (WalletId, AccountIndex, AddressType, AddressIndex, OutputTxId, OutputIndex, ScriptPubKey) IN (
                    SELECT  WalletId, AccountIndex, AddressType, AddressIndex, OutputTxId, OutputIndex, ScriptPubKey
                    FROM    temp.TxToDelete)"))
            {
                removedTxs[td.OutputTxId] = td.OutputTxTime;
            }

            this.Execute($@"
            DELETE  FROM HDTransactionData
            WHERE   (WalletId, AccountIndex, AddressType, AddressIndex, OutputTxId, OutputIndex, ScriptPubKey) IN (
                    SELECT  WalletId, AccountIndex, AddressType, AddressIndex, OutputTxId, OutputIndex, ScriptPubKey
                    FROM    temp.TxToDelete)");

            foreach (HDTransactionData td in this.Query<HDTransactionData>($@"
            SELECT  *
            FROM    HDTransactionData
            {spendFilter}"))
            {
                removedTxs[td.SpendTxId] = (long)td.SpendTxTime;
            }

            this.Execute($@"
            UPDATE  HDTransactionData
            SET     SpendBlockHeight = NULL
            ,       SpendBlockHash = NULL
            ,       SpendTxTime = NULL
            ,       SpendTxId = NULL
            ,       SpendTxIsCoinBase = NULL
            ,       SpendTxTotalOut = NULL
            {spendFilter}");

            return removedTxs.Select(kv => (kv.Key, kv.Value));
        }

        internal long? RemoveUnconfirmedTransaction(int walletId, uint256 txId)
        {
            string outputFilter = $@"
            WHERE   OutputTxId = '{txId}'
            AND     OutputBlockHeight IS NULL
            AND     OutputBlockHash IS NULL
            AND     WalletId = {walletId}";

            string spendFilter = $@"
            WHERE   SpendTxId = '{txId}'
            AND     SpendBlockHeight IS NULL
            AND     SpendBlockHash IS NULL
            AND     WalletId = {walletId}";

            var res = this.RemoveTransactionsByTxToDelete(outputFilter, spendFilter);

            return !res.Any() ? (long?)null : res.First().unixTimeSeconds;
        }

        internal IEnumerable<(string txId, long creationTime)> RemoveAllUnconfirmedTransactions(int walletId)
        {
            string outputFilter = $@"
            WHERE   OutputBlockHeight IS NULL
            AND     OutputBlockHash IS NULL
            AND     WalletId = {walletId}";

            string spendFilter = $@"
            WHERE   SpendBlockHeight IS NULL
            AND     SpendBlockHash IS NULL
            AND     WalletId = {walletId}";

            var res = this.RemoveTransactionsByTxToDelete(outputFilter, spendFilter);

            return res;
        }

        internal IEnumerable<(string txId, long unixTimeSeconds)> RemoveTransactionsAfterLastBlockSynced(int lastBlockSyncedHeight, int? walletId = null)
        {
            string outputFilter = (walletId == null) ? $@"
            WHERE   OutputBlockHeight > {lastBlockSyncedHeight}" : $@"
            WHERE   WalletId = {walletId}
            AND     OutputBlockHeight > {lastBlockSyncedHeight}";

            string spendFilter = (walletId == null) ? $@"
            WHERE   SpendBlockHeight > {lastBlockSyncedHeight}" : $@"
            WHERE   WalletId = {walletId}
            AND     SpendBlockHeight > {lastBlockSyncedHeight}";

            return this.RemoveTransactionsByTxToDelete(outputFilter, spendFilter);
        }

        /// <summary>
        /// Only keep wallet transactions up to and including the specified block.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <param name="lastBlockSynced">The last block synced to set.</param>
        internal IEnumerable<(string txId, long creationTime)> SetLastBlockSynced(HDWallet wallet, ChainedHeader lastBlockSynced)
        {
            if (this.IsInTransaction)
            {
                this.AddRollbackAction(new
                {
                    wallet.Name,
                    wallet.LastBlockSyncedHeight,
                    wallet.LastBlockSyncedHash,
                    wallet.BlockLocator
                }, (dynamic rollBackData) =>
                {
                    HDWallet wallet2 = this.Repository.Wallets[rollBackData.Name];
                    wallet2.LastBlockSyncedHash = rollBackData.LastBlockSyncedHash;
                    wallet2.LastBlockSyncedHeight = rollBackData.LastBlockSyncedHeight;
                    wallet2.BlockLocator = rollBackData.BlockLocator;
                });
            }

            Guard.Assert(this.IsInTransaction);

            IEnumerable<(string txId, long creationTime)> res = this.RemoveTransactionsAfterLastBlockSynced(lastBlockSynced?.Height ?? -1, wallet.WalletId);
            wallet.SetLastBlockSynced(lastBlockSynced, this.Repository.Network);
            this.SQLiteConnection.Update(wallet);

            return res;
        }

        internal void ProcessTransactions(IEnumerable<IEnumerable<string>> tableScripts, HDWallet wallet, ChainedHeader newLastSynced = null, uint256 prevTipHash = null)
        {
            Guard.Assert(this.IsInTransaction);

            // Execute the scripts providing the temporary tables to merge with the wallet tables.
            foreach (IEnumerable<string> tableScript in tableScripts)
                foreach (string command in tableScript)
                    this.Execute(command);

            // Inserts or updates HDTransactionData records based on change or funds received.
            string walletName = wallet?.Name;
            string prevHash = prevTipHash?.ToString();

            // Check for spending overlaps.
            // Performs checks that we do not affect a confirmed transaction's spends.
            var cmdUpdateOverlaps = this.Commands["CmdUpdateOverlaps"];
            cmdUpdateOverlaps.Bind("walletName", walletName);
            cmdUpdateOverlaps.Bind("prevHash", prevHash);
            List<HDTransactionData> overlaps = cmdUpdateOverlaps.ExecuteQuery<HDTransactionData>();
            foreach ((int walletId, string txId) in overlaps.Where(o => o.SpendBlockHash == null).Select(o => (o.WalletId, o.SpendTxId)).Distinct())
            {
                this.RemoveUnconfirmedTransaction(walletId, uint256.Parse(txId));
            }

            DBCommand cmdUploadPrevOut = this.Commands["CmdUploadPrevOut"];
            cmdUploadPrevOut.Bind("walletName", walletName);
            cmdUploadPrevOut.Bind("prevHash", prevHash);
            cmdUploadPrevOut.ExecuteNonQuery();

            // Insert the HDPayment records.
            // Performs checks that we do not affect a confirmed transaction's payments.
            DBCommand cmdReplacePayments = this.Commands["CmdReplacePayments"];
            cmdReplacePayments.Bind("walletName", walletName);
            cmdReplacePayments.Bind("prevHash", prevHash);
            cmdReplacePayments.ExecuteNonQuery();

            // Update spending details on HDTransactionData records.
            // Performs checks that we do not affect a confirmed transaction's spends.
            var cmdUpdateSpending = this.Commands["CmdUpdateSpending"];
            cmdUpdateSpending.Bind("walletName", walletName);
            cmdUpdateSpending.Bind("prevHash", prevHash);
            cmdUpdateSpending.ExecuteNonQuery();

            // Advance participating wallets.
            if (newLastSynced != null)
                HDWallet.AdvanceTip(this, wallet, newLastSynced, prevTipHash);
        }
    }
}
