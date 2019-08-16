using System.Collections.Generic;
using System.IO;
using NBitcoin;
using NBitcoin.DataEncoders;
using SQLite;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// This class represents a connection to the repository. Its a central point for all functionality that can be performed via a connection.
    /// </summary>
    public class DBConnection : SQLiteConnection
    {
        public SQLiteWalletRepository repo;

        public DBConnection(SQLiteWalletRepository repo, string dbFile) : base(Path.Combine(repo.DBPath, dbFile))
        {
            this.repo = repo;
        }

        internal void CreateDBStructure()
        {
            this.CreateTable<HDWallet>();
            this.CreateTable<HDAccount>();
            this.CreateTable<HDAddress>();
            this.CreateTable<HDTransactionData>();
            this.CreateTable<HDPayment>();
        }

        internal ObjectsOfInterest DetermineObjectsOfInterest(int? walletId)
        {
            var res = new ObjectsOfInterest();

            List<HDAddress> addresses = this.Query<HDAddress>($@"
                SELECT  *
                FROM    HDAddress {((walletId != null) ? $@"
                WHERE   WalletId = {walletId}":"")}");

            foreach (HDAddress address in addresses)
                res.Add(Encoders.Hex.DecodeData(address.ScriptPubKey));

            List<HDTransactionData> spendableTransactions = this.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   SpendBlockHash IS NULL
                AND     SpendBlockHeight IS NULL{((walletId != null) ? $@"
                AND      WalletId = {walletId}" : "")}");

            foreach (HDTransactionData transactonData in spendableTransactions)
            {
                uint256 hash = uint256.Parse(transactonData.OutputTxId);
                res.Add(hash.ToBytes());
            }

            return res;
        }

        internal List<HDAddress> CreateAddresses(HDAccount account, int addressType, int addressesQuantity)
        {
            var addresses = new List<HDAddress>();

            int addressCount = HDAddress.GetAddressCount(this, account.WalletId, account.AccountIndex, addressType);

            for (int addressIndex = addressCount; addressIndex < (addressCount + addressesQuantity); addressIndex++)
                addresses.Add(CreateAddress(account, addressType, addressIndex));

            return addresses;
        }

        internal void TopUpAddresses(int walletId, int accountIndex, int addressType)
        {
            int addressCount = HDAddress.GetAddressCount(this, walletId, accountIndex, addressType);
            int nextAddressIndex = HDAddress.GetNextAddressIndex(this, walletId, accountIndex, addressType);
            int buffer = addressCount - nextAddressIndex;

            var account = HDAccount.GetAccount(this, walletId, accountIndex);

            for (int addressIndex = addressCount; buffer < 20; buffer++, addressIndex++)
                CreateAddress(account, addressType, addressIndex);
        }

        internal HDAddress CreateAddress(HDAccount account, int addressType, int addressIndex)
        {
            // Retrieve the pubkey associated with the private key of this address index.
            var keyPath = new KeyPath($"{addressType}/{addressIndex}");

            ExtPubKey extPubKey = ExtPubKey.Parse(account.ExtPubKey, this.repo.Network).Derive(keyPath);
            PubKey pubKey = extPubKey.PubKey;
            Script pubKeyScript = pubKey.ScriptPubKey;
            Script scriptPubKey = this.repo.ScriptPubKeyProvider.FromPubKey(pubKey, account.ScriptPubKeyType);

            // Add the new address details to the list of addresses.
            return this.CreateAddress(account, addressType, addressIndex, pubKeyScript.ToHex(), scriptPubKey.ToHex());
        }

        internal HDAddress CreateAddress(HDAccount account, int addressType, int addressIndex, string pubKey, string scriptPubKey)
        {
            // Add the new address details to the list of addresses.
            var newAddress = new HDAddress
            {
                WalletId = account.WalletId,
                AccountIndex = account.AccountIndex,
                AddressType = addressType,
                AddressIndex = addressIndex,
                PubKey = pubKey,
                ScriptPubKey = scriptPubKey
            };

            this.Insert(newAddress);

            return newAddress;
        }

        internal IEnumerable<HDAddress> GetUsedAddresses(int walletId, int accountIndex, int addressType)
        {
            return HDAddress.GetUsedAddresses(this, walletId, accountIndex, addressType);
        }

        internal HDAccount CreateAccount(int walletId, int accountIndex, string accountName, string extPubKey, string scriptPubKeyType, int creationTimeSeconds)
        {
            var account = new HDAccount()
            {
                WalletId = walletId,
                AccountIndex = accountIndex,
                AccountName = accountName,
                ExtPubKey = extPubKey,
                CreationTime = creationTimeSeconds,
                ScriptPubKeyType = scriptPubKeyType
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
            CREATE  TABLE temp.TxToDelete (
                    WalletId INT
            ,       AccountIndex INT
            ,       AddressType INT
            ,       AddressIndex INT
            ,       ScriptPubKey TEXT)");

            this.Execute($@"
            INSERT  INTO temp.TxToDelete (
                    WalletId
            ,       AccountIndex
            ,       AddressType
            ,       AddressIndex
            ,       ScriptPubKey)
            SELECT  WalletId
            ,       AccountIndex
            ,       AddressType
            ,       AddressIndex
            ,       ScriptPubKey
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
            WHERE   (WalletId, AccountIndex, AddressType, AddressIndex, ScriptPubKey) IN (
                    SELECT  WalletId, AccountIndex, AddressType, AddressIndex, ScriptPubKey
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
            wallet.SetLastBlockSynced(lastBlockSynced);
            this.RemoveTransactionsAfterLastBlockSynced(wallet.LastBlockSyncedHeight, wallet.WalletId);
            this.Update(wallet);
        }

        internal void ProcessTransactions(ChainedHeader header = null, string walletName = null)
        {
            while (true)
            {
                // Determines the HDTransactionData records that will be updated.
                // Only unconfirmed transactions are selected for update.
                List<HDTransactionData> hdTransactions = this.Query<HDTransactionData>($@"
                    SELECT A.WalletID
                    ,      A.AccountIndex
                    ,      A.AddressType
                    ,      A.AddressIndex
                    ,      A.ScriptPubKey
                    ,      A.PubKey
                    ,      T.Value
                    ,      T.OutputBlockHeight
                    ,      T.OutputBlockHash
                    ,      T.OutputTxIsCoinBase
                    ,      T.OutputTxTime
                    ,      T.OutputTxId
                    ,      T.OutputIndex
                    ,      NULL
                    ,      NULL
                    ,      NULL
                    ,      NULL
                    ,      NULL
                    ,      NULL
                    ,      NULL
                    FROM   temp.TempOutput T
                    JOIN   HDAddress A
                    ON     A.ScriptPubKey = T.ScriptPubKey
                    JOIN   HDWallet W
                    ON     W.WalletId = A.WalletId
                    {/* Respect the wallet name if provided */((walletName != null) ? $@"
                    AND    W.Name = '{walletName}'" : "")}
                    {/* Restrict non-transient transaction updates to aligned wallets */((header != null) ? $@"
                    AND    W.LastBlockSyncedHash = '{(header.Previous?.HashBlock ?? uint256.Zero)}'" : "")}
                    LEFT   JOIN HDTransactionData TD
                    ON     TD.WalletId = A.WalletId
                    AND    TD.AccountIndex  = A.AccountIndex
                    AND    TD.AddressType = A.AddressType
                    AND    TD.AddressIndex = A.AddressIndex
                    AND    TD.OutputTxId = T.OutputTxId
                    AND    TD.OutputIndex = T.OutputIndex
                    WHERE  TD.OutputBlockHash IS NULL
                    AND    TD.OutputBlockHeight IS NULL
                    ORDER  BY A.WalletId, A.AccountIndex, A.AddressType, A.AddressIndex, T.OutputTxId, T.OutputIndex");

                if (hdTransactions.Count == 0)
                    break;

                var topUpRequired = new HashSet<(int walletId, int accountIndex, int addressType)>();

                // We will go through the sorted list and make some updates each time the address changes.
                (int walletId, int accountIndex, int addressType, int addressIndex) current = (-1, -1, -1, -1);
                (int walletId, int accountIndex, int addressType, int addressIndex) prev = (-1, -1, -1, -1);

                // Now go through the HDTransaction data records.
                HDAccount hdAccount = null;

                foreach (HDTransactionData hdTransactionData in hdTransactions)
                {
                    current = (hdTransactionData.WalletId, hdTransactionData.AccountIndex, hdTransactionData.AddressType, hdTransactionData.AddressIndex);

                    // If the account changed then invalidate the current object.
                    if (prev.walletId != current.walletId || prev.accountIndex != current.accountIndex)
                        hdAccount = null;

                    // About to use an address for the first time?
                    int transactionCount = HDAddress.GetTransactionCount(this, current.walletId, current.accountIndex, current.addressType, current.addressIndex);
                    if (transactionCount == 0)
                    {
                        if (hdAccount == null)
                            hdAccount = HDAccount.GetAccount(this, current.walletId, current.accountIndex);
                        if (!string.IsNullOrEmpty(hdAccount.ScriptPubKeyType))
                            topUpRequired.Add((current.walletId, current.accountIndex, current.addressType));
                    }

                    this.InsertOrReplace(hdTransactionData);

                    prev = current;
                }

                if (topUpRequired.Count == 0)
                    break;

                foreach ((int walletId, int accountIndex, int addressType) in topUpRequired)
                    this.TopUpAddresses(walletId, accountIndex, addressType);
            }

            // Clear the payments since we are replacing them.
            // Performs checks that we do not clear a confirmed transaction's payments.
            this.Execute($@"
                DELETE  FROM HDPayment
                WHERE   (OutputTxTime, OutputTxId, OutputIndex) IN (
                        SELECT  TD.OutputTxTime, T.OutputTxId, T.OutputIndex
                        FROM    temp.TempPrevOut T
                        JOIN    HDTransactionData TD
                        ON      TD.OutputTxId = T.OutputTxId
                        AND     TD.OutputIndex = T.OutputIndex
                        AND     TD.SpendBlockHeight IS NULL
                        AND     TD.SpendBlockHash IS NULL
                        JOIN    HDWallet W
                        ON      W.WalletId = TD.WalletId
                        {/* Respect the wallet name if provided */((walletName != null) ? $@"
                        AND     W.Name = '{walletName}'" : "")}
                        {/* Restrict non-transient transaction updates to aligned wallets */((header != null) ? $@"
                        AND     W.LastBlockSyncedHash = '{(header.Previous?.HashBlock ?? uint256.Zero)}'" : "")}
                        )");

            // Insert spending details into HDPayment records.
            // Performs checks that we do not affect a confirmed transaction's payments.
            this.Execute($@"
                REPLACE INTO HDPayment
                SELECT  TD.OutputTxTime
                ,       TD.OutputTxId
                ,       TD.OutputIndex
                ,       O.OutputIndex
                ,       O.ScriptPubKey
                ,       O.Value
                FROM    temp.TempPrevOut T
                JOIN    HDTransactionData TD
                ON      TD.OutputTxId = T.OutputTxId
                AND     TD.OutputIndex = T.OutputIndex
                AND     TD.SpendBlockHeight IS NULL
                AND     TD.SpendBlockHash IS NULL
                JOIN   HDWallet W
                ON     W.WalletId = TD.WalletId
                {/* Respect the wallet name if provided */((walletName != null) ? $@"
                AND     W.Name = '{walletName}'" : "")}
                {/* Restrict non-transient transaction updates to aligned wallets */((header != null) ? $@"
                AND     W.LastBlockSyncedHash = '{(header.Previous?.HashBlock ?? uint256.Zero)}'" : "")}
                JOIN    temp.TempOutput O
                ON      O.OutputTxID = T.SpendTxId");

            // Update spending details on HDTransactionData records.
            // Performs checks that we do not affect a confirmed transaction's spends.
            this.Execute($@"
                REPLACE INTO HDTransactionData
                SELECT TD.WalletId
                ,      TD.AccountIndex
                ,      TD.AddressType
                ,      TD.AddressIndex
                ,      TD.ScriptPubKey
                ,      TD.PubKey
                ,      TD.Value
                ,      TD.OutputBlockHeight
                ,      TD.OutputBlockHash
                ,      TD.OutputTxIsCoinBase
                ,      TD.OutputTxTime
                ,      TD.OutputTxId
                ,      TD.OutputIndex
                ,      T.SpendBlockHeight
                ,      T.SpendBlockHash
                ,      T.SpendTxIsCoinBase
                ,      T.SpendTxTime
                ,      T.SpendTxId
                ,      T.SpendTxTotalOut
                FROM   temp.TempPrevOut T
                JOIN   HDTransactionData TD
                ON     TD.OutputTxID = T.OutputTxId
                AND    TD.OutputIndex = T.OutputIndex
                AND    TD.SpendBlockHeight IS NULL
                AND    TD.SpendBlockHash IS NULL
                JOIN   HDWallet W
                ON     W.WalletId = TD.WalletId
                {/* Respect the wallet name if provided */((walletName != null) ? $@"
                AND     W.Name = '{walletName}'" : "")}
                {/* Restrict non-transient transaction updates to aligned wallets */((header != null) ? $@"
                AND     W.LastBlockSyncedHash = '{(header.Previous?.HashBlock ?? uint256.Zero)}'" : "")}
                ORDER BY TD.WalletId
                ,      TD.AccountIndex
                ,      TD.AddressType
                ,      TD.AddressIndex
                ,      TD.ScriptPubKey
                ");

            // Advance participating wallets.
            if (header != null)
            {
                this.Execute($@"
                    UPDATE HDWallet
                    SET    LastBlockSyncedHash = '{header.HashBlock}',
                           LastBlockSyncedHeight = {header.Height},
                           BlockLocator = '{string.Join(",", header.GetLocator().Blocks)}'
                    WHERE  LastBlockSyncedHash = '{(header.Previous?.HashBlock ?? uint256.Zero)}'
                    {/* Respect the wallet name if provided */((walletName != null) ? $@"
                    AND    Name = '{walletName}'" : "")}");
            }
        }
    }
}
