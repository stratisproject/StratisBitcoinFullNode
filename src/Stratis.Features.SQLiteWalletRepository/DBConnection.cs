using System;
using System.Collections.Generic;
using System.IO;
using NBitcoin;
using SQLite;
using Stratis.Features.SQLiteWalletRepository.Commands;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// This class represents a connection to the repository. Its a central point for all functionality that can be performed via a connection.
    /// </summary>
    public class DBConnection
    {
        private SQLiteConnection sqLiteConnection;
        public SQLiteWalletRepository Repository;
        public Stack<(dynamic, Action<dynamic>)> RollBackActions;
        public Dictionary<string, SQLiteCommand> Commands;

        // A given connection can't have two transactions running in parallel.
        internal readonly object TransactionLock;
        internal int TransactionDepth;
        internal bool IsInTransaction => this.sqLiteConnection.IsInTransaction;

        internal Dictionary<string, long> Metrics = new Dictionary<string, long>();

        internal SQLiteCommand CmdUploadPrevOut;
        internal SQLiteCommand CmdDeletePayments;
        internal SQLiteCommand CmdReplacePayments;
        internal SQLiteCommand CmdUpdateSpending;

        public DBConnection(SQLiteWalletRepository repo, string dbFile)
        {
            this.sqLiteConnection = new SQLiteConnection(Path.Combine(repo.DBPath, dbFile));
            this.Repository = repo;
            this.TransactionLock = new object();
            this.TransactionDepth = 0;
            this.RollBackActions = new Stack<(object, Action<object>)>();

            this.CmdUploadPrevOut = this.CmdUploadPrevOut();
            this.CmdDeletePayments = this.CmdDeletePayments();
            this.CmdReplacePayments = this.CmdReplacePayments();
            this.CmdUpdateSpending = this.CmdUpdateSpending();
        }

        internal void AddRollbackAction(object rollBackData, Action<object> rollBackAction)
        {
            this.RollBackActions.Push((rollBackData, rollBackAction));
        }

        public static implicit operator SQLiteConnection(DBConnection d) => d.sqLiteConnection;

        internal void BeginTransaction()
        {
            lock (this.TransactionLock)
            {
                if (this.TransactionDepth == 0)
                    this.sqLiteConnection.BeginTransaction();

                this.TransactionDepth++;
            }
        }

        internal void Rollback()
        {
            lock (this.TransactionLock)
            {
                this.TransactionDepth--;

                if (this.TransactionDepth == 0)
                {
                    this.sqLiteConnection.Rollback();

                    while (this.RollBackActions.Count > 0)
                    {
                        (dynamic rollBackData, Action<dynamic> rollBackAction) = this.RollBackActions.Pop();

                        rollBackAction(rollBackData);
                    }
                }
            }
        }

        internal void Commit()
        {
            lock (this.TransactionLock)
            {
                this.TransactionDepth--;

                if (this.TransactionDepth == 0)
                {
                    this.sqLiteConnection.Commit();
                    this.RollBackActions.Clear();
                }
            }
        }

        internal SQLiteCommand CreateCommand(string cmdText, params object[] ps)
        {
            return this.sqLiteConnection.CreateCommand(cmdText, ps);
        }

        internal List<T> Query<T>(string query, params object[] args) where T:new()
        {
            return this.sqLiteConnection.Query<T>(query, args);
        }

        internal void Insert(object obj)
        {
            this.sqLiteConnection.Insert(obj);
        }

        internal void Delete<T>(object obj)
        {
            this.sqLiteConnection.Delete<T>(obj);
        }

        internal void InsertOrReplace(object obj)
        {
            this.sqLiteConnection.InsertOrReplace(obj);
        }

        internal T Find<T>(object pk) where T : new()
        {
            return this.sqLiteConnection.Find<T>(pk);
        }

        internal T FindWithQuery<T>(string query, params object[] args) where T : new()
        {
            return this.sqLiteConnection.FindWithQuery<T>(query, args);
        }

        internal void Execute(string query, params object[] args)
        {
            this.sqLiteConnection.Execute(query, args);
        }

        internal T ExecuteScalar<T>(string query, params object[] args) where T : new()
        {
            return this.sqLiteConnection.ExecuteScalar<T>(query, args);
        }

        internal void Close()
        {
            this.sqLiteConnection.Close();
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

            int addressCount = HDAddress.GetAddressCount(this.sqLiteConnection, account.WalletId, account.AccountIndex, addressType);
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

            int addressCount = HDAddress.GetAddressCount(this.sqLiteConnection, account.WalletId, account.AccountIndex, addressType);

            for (int addressIndex = addressCount; addressIndex < (addressCount + addressesQuantity); addressIndex++)
            {
                HDAddress address = CreateAddress(account, addressType, addressIndex);
                this.Insert(address);
                addresses.Add(address);
            }

            return addresses;
        }

        internal IEnumerable<HDAddress> TopUpAddresses(int walletId, int accountIndex, int addressType)
        {
            int addressCount = HDAddress.GetAddressCount(this.sqLiteConnection, walletId, accountIndex, addressType);
            int nextAddressIndex = HDAddress.GetNextAddressIndex(this, walletId, accountIndex, addressType);
            int buffer = addressCount - nextAddressIndex;

            var account = HDAccount.GetAccount(this, walletId, accountIndex);

            for (int addressIndex = addressCount; buffer < HDAddress.StandardAddressBuffer; buffer++, addressIndex++)
            {
                HDAddress address = CreateAddress(account, addressType, addressIndex);
                this.Insert(address);
                yield return address;
            }
        }

        internal HDAddress CreateAddress(HDAccount account, int addressType, int addressIndex)
        {
            // Retrieve the pubkey associated with the private key of this address index.
            var keyPath = new KeyPath($"{addressType}/{addressIndex}");

            ExtPubKey extPubKey = account.GetExtPubKey(this.Repository.Network).Derive(keyPath);
            PubKey pubKey = extPubKey.PubKey;
            Script pubKeyScript = pubKey.ScriptPubKey;
            Script scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(pubKey);

            // Add the new address details to the list of addresses.
            return new HDAddress()
            {
                WalletId = account.WalletId,
                AccountIndex = account.AccountIndex,
                AddressType = addressType,
                AddressIndex = addressIndex,
                PubKey = pubKeyScript.ToHex(),
                ScriptPubKey = scriptPubKey.ToHex()
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
            WHERE  (OutputTxTime, OutputTxId, OutputIndex) IN (
                    SELECT OutputTxTime
                    ,      OutputTxId
                    ,      OutputIndex
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
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="lastBlockSynced">The last block synced to set.</param>
        internal void SetLastBlockSynced(string walletName, ChainedHeader lastBlockSynced)
        {
            var wallet = this.GetWalletByName(walletName);

            if (this.IsInTransaction)
            {
                this.RollBackActions.Push((new {
                    wallet.Name,
                    wallet.LastBlockSyncedHeight,
                    wallet.LastBlockSyncedHash,
                    wallet.BlockLocator }, (rollBackData) =>
                {
                    HDWallet wallet2 = this.GetWalletByName(rollBackData.Name);
                    wallet2.LastBlockSyncedHash = rollBackData.LastBlockSyncedHash;
                    wallet2.LastBlockSyncedHeight = rollBackData.LastBlockSyncedHeight;
                    wallet2.BlockLocator = rollBackData.BlockLocator;
                }));
            }

            this.RemoveTransactionsAfterLastBlockSynced(lastBlockSynced?.Height ?? -1, wallet.WalletId);
            wallet.SetLastBlockSynced(lastBlockSynced);
            this.sqLiteConnection.Update(wallet);
        }

        internal void ProcessTransactions(IEnumerable<IEnumerable<string>> tableScripts, HDWallet wallet, ChainedHeader header = null, AddressesOfInterest addressesOfInterest = null)
        {
            // Execute the scripts providing the temporary tables to merge with the wallet tables.
            foreach (IEnumerable<string> tableScript in tableScripts)
                foreach (string command in tableScript)
                    this.Execute(command);

            // Inserts or updates HDTransactionData records based on change or funds received.
            string walletName = wallet?.Name;
            string prevHash = (header == null) ? null : (header.Previous?.HashBlock ?? uint256.Zero).ToString();
            this.CmdUploadPrevOut.Bind("walletName", walletName);
            this.CmdUploadPrevOut.Bind("prevHash", prevHash);
            this.CmdUploadPrevOut.ExecuteNonQuery();

            // Clear the payments since we are replacing them.
            // Performs checks that we do not clear a confirmed transaction's payments.
            this.CmdDeletePayments.Bind("walletName", walletName);
            this.CmdDeletePayments.Bind("prevHash", prevHash);
            this.CmdDeletePayments.ExecuteNonQuery();

            // Insert spending details into HDPayment records.
            // Performs checks that we do not affect a confirmed transaction's payments.
            this.CmdReplacePayments.Bind("walletName", walletName);
            this.CmdReplacePayments.Bind("prevHash", prevHash);
            this.CmdReplacePayments.ExecuteNonQuery();

            // Update spending details on HDTransactionData records.
            // Performs checks that we do not affect a confirmed transaction's spends.
            this.CmdUpdateSpending.Bind("walletName", walletName);
            this.CmdUpdateSpending.Bind("prevHash", prevHash);
            this.CmdUpdateSpending.ExecuteNonQuery();

            // Advance participating wallets.
            if (header != null)
                HDWallet.AdvanceTip(this, wallet, header, header.Previous);
        }
    }
}
