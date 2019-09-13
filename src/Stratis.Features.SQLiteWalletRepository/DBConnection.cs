using System;
using System.Collections.Generic;
using System.IO;
using NBitcoin;
using SQLite;
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
            this.SQLiteConnection = new SQLiteConnection(Path.Combine(repo.DBPath, dbFile));
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
                this.TransactionDepth = 0;
            }

            this.TransactionDepth++;
        }

        internal void Rollback()
        {
            this.TransactionDepth--;

            if (this.TransactionDepth == 0 && this.SQLiteConnection.IsInTransaction)
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
            this.TransactionDepth--;

            if (this.TransactionDepth == 0 && this.SQLiteConnection.IsInTransaction)
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

        internal List<HDAddress> AddAdresses(HDAccount account, int addressType, List<Script> scriptPubKeys)
        {
            var addresses = new List<HDAddress>();

            int addressCount = HDAddress.GetAddressCount(this.SQLiteConnection, account.WalletId, account.AccountIndex, addressType);
            int addressIndex = addressCount;

            for (int i= 0; i < scriptPubKeys.Count; addressIndex++, i++)
            {
                HDAddress address = CreateAddress(account, addressType, addressIndex);
                address.ScriptPubKey = scriptPubKeys[i].ToHex();
                this.Insert(address);
                addresses.Add(address);
            }

            return addresses;
        }

        internal List<HDAddress> CreateAddresses(HDAccount account, int addressType, int addressesQuantity)
        {
            var addresses = new List<HDAddress>();

            int addressCount = HDAddress.GetAddressCount(this.SQLiteConnection, account.WalletId, account.AccountIndex, addressType);

            for (int addressIndex = addressCount; addressIndex < (addressCount + addressesQuantity); addressIndex++)
            {
                HDAddress address = CreateAddress(account, addressType, addressIndex);
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

        internal IEnumerable<HDAddress> GetUsedAddresses(int walletId, int accountIndex, int addressType)
        {
            return HDAddress.GetUsedAddresses(this, walletId, accountIndex, addressType);
        }

        internal HDAccount CreateAccount(int walletId, int accountIndex, string accountName, string extPubKey, int creationTimeSeconds)
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

        internal IEnumerable<HDAccount> GetAccounts(int walletId)
        {
            return HDAccount.GetAccounts(this, walletId);
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

        private void RemoveTransactionsByTxToDelete(string outputFilter, string spendFilter)
        {
            this.Execute($@"
            DROP    TABLE IF EXISTS temp.TxToDelete");

            this.Execute($@"
            CREATE  TABLE temp.TxToDelete (
                    WalletId INT
            ,       AccountIndex INT
            ,       AddressType INT
            ,       AddressIndex INT
            ,       RedeemScript TEXT)");

            this.Execute($@"
            INSERT  INTO temp.TxToDelete (
                    WalletId
            ,       AccountIndex
            ,       AddressType
            ,       AddressIndex
            ,       RedeemScript)
            SELECT  WalletId
            ,       AccountIndex
            ,       AddressType
            ,       AddressIndex
            ,       RedeemScript
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

            this.Execute($@"
            DELETE  FROM HDTransactionData
            WHERE   (WalletId, AccountIndex, AddressType, AddressIndex, RedeemScript) IN (
                    SELECT  WalletId, AccountIndex, AddressType, AddressIndex, RedeemScript
                    FROM    temp.TxToDelete)");

            this.Execute($@"
            UPDATE  HDTransactionData
            SET     SpendBlockHeight = NULL
            ,       SpendBlockHash = NULL
            ,       SpendTxTime = NULL
            ,       SpendTxId = NULL
            ,       SpendTxIsCoinBase = NULL
            ,       SpendTxTotalOut = NULL
            {spendFilter}");
        }

        internal void RemoveUnconfirmedTransaction(int walletId, uint256 txId)
        {
            string outputFilter = $@"
            WHERE   OutputTxId = '{txId}'
            AND     OutputBlockHeight IS NULL
            AND     OutputBlockHash IS NULL
            AND     WalletId = {walletId}";

            string spendFilter = $@"
            WHERE   SpendTxId = '{txId}'
            AND     SpendBlockHeight IS NULL
            AND     SpendBlockHash IS NULL";

            this.RemoveTransactionsByTxToDelete(outputFilter, spendFilter);
        }

        internal void RemoveTransactionsAfterLastBlockSynced(int lastBlockSyncedHeight, int? walletId = null)
        {
            string outputFilter = (walletId == null) ? $@"
            WHERE   OutputBlockHeight > {lastBlockSyncedHeight}" : $@"
            WHERE   WalletId = {walletId}
            AND     OutputBlockHeight > {lastBlockSyncedHeight}";

            string spendFilter = (walletId == null) ? $@"
            WHERE   SpendBlockHeight > {lastBlockSyncedHeight}" : $@"
            WHERE   WalletId = {walletId}
            AND     SpendBlockHeight > {lastBlockSyncedHeight}";

            this.RemoveTransactionsByTxToDelete(outputFilter, spendFilter);
        }

        /// <summary>
        /// Only keep wallet transactions up to and including the specified block.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <param name="lastBlockSynced">The last block synced to set.</param>
        internal void SetLastBlockSynced(HDWallet wallet, ChainedHeader lastBlockSynced)
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

            this.RemoveTransactionsAfterLastBlockSynced(lastBlockSynced?.Height ?? -1, wallet.WalletId);
            wallet.SetLastBlockSynced(lastBlockSynced, this.Repository.Network);
            this.SQLiteConnection.Update(wallet);
        }

        internal void ProcessTransactions(IEnumerable<IEnumerable<string>> tableScripts, HDWallet wallet, ChainedHeader newLastSynced = null, HashHeightPair prevLastSynced = null)
        {
            // Execute the scripts providing the temporary tables to merge with the wallet tables.
            foreach (IEnumerable<string> tableScript in tableScripts)
                foreach (string command in tableScript)
                    this.Execute(command);

            // Inserts or updates HDTransactionData records based on change or funds received.
            string walletName = wallet?.Name;
            string prevHash = (prevLastSynced?.Hash ?? uint256.Zero).ToString();

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
                HDWallet.AdvanceTip(this, wallet, newLastSynced, prevLastSynced);
        }
    }
}
