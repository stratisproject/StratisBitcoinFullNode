using System.Collections.Generic;
using SQLite;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class HDTransactionData
    {
        public int WalletId { get; set; }
        public int AccountIndex { get; set; }
        public int AddressType { get; set; }
        public int AddressIndex { get; set; }
        public string RedeemScript { get; set; }
        public string ScriptPubKey { get; set; }
        public string Address { get; set; }
        public decimal Value { get; set; }
        public long OutputTxTime { get; set; }
        public string OutputTxId { get; set; }
        public int OutputIndex { get; set; }
        public int? OutputBlockHeight { get; set; }
        public string OutputBlockHash { get; set; }
        public int OutputTxIsCoinBase { get; set; }
        public long? SpendTxTime { get; set; }
        public string SpendTxId { get; set; }
        public int? SpendBlockHeight { get; set; }
        public int SpendTxIsCoinBase { get; set; }
        public string SpendBlockHash { get; set; }
        public decimal? SpendTxTotalOut { get; set; }

        internal static IEnumerable<string> CreateScript()
        {
            yield return $@"
            CREATE TABLE HDTransactionData (
                WalletId            INTEGER NOT NULL,
                AccountIndex        INTEGER NOT NULL,
                AddressType         INTEGER NOT NULL,
                AddressIndex        INTEGER NOT NULL,
                RedeemScript        TEXT NOT NULL,
                ScriptPubKey        TEXT NOT NULL,
                Address             TEXT NOT NULL,
                Value               DECIMAL NOT NULL,
                OutputBlockHeight   INTEGER,
                OutputBlockHash     TEXT,
                OutputTxIsCoinBase  INTEGER NOT NULL,
                OutputTxTime        INTEGER NOT NULL,
                OutputTxId          TEXT NOT NULL,
                OutputIndex         INTEGER NOT NULL,
                SpendBlockHeight    INTEGER,
                SpendBlockHash      TEXT,
                SpendTxIsCoinBase   INTEGER,
                SpendTxTime         INTEGER,
                SpendTxId           TEXT,
                SpendTxTotalOut     DECIMAL,
                PRIMARY KEY(WalletId, AccountIndex, AddressType, AddressIndex, OutputTxId, OutputIndex)
            )";

            yield return "CREATE UNIQUE INDEX UX_HDTransactionData_Output ON HDTransactionData(WalletId, OutputTxId, OutputIndex, ScriptPubKey)";
            yield return "CREATE INDEX IX_HDTransactionData_SpendTxTime ON HDTransactionData (WalletId, AccountIndex, SpendTxTime)";
            yield return "CREATE INDEX IX_HDTransactionData_OutputTxTime ON HDTransactionData (WalletId, AccountIndex, OutputTxTime, OutputIndex)";
        }

        internal static void CreateTable(SQLiteConnection conn)
        {
            foreach (string command in CreateScript())
                conn.Execute(command);
        }

        internal static IEnumerable<HDTransactionData> GetAllTransactions(DBConnection conn, int walletId, int? accountIndex, int? addressType, int? addressIndex, int limit = int.MaxValue, HDTransactionData prev = null, bool descending = true)
        {
            string strWalletId = DBParameter.Create(walletId);
            string strAccountIndex = DBParameter.Create(accountIndex);
            string strAddressType = DBParameter.Create(addressType);
            string strAddressIndex = DBParameter.Create(addressIndex);
            string strLimit = DBParameter.Create(limit);
            string strPrevTime = DBParameter.Create(prev?.OutputTxTime);
            string strPrevIndex = DBParameter.Create(prev?.OutputIndex);

            return conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   WalletId = {strWalletId} {((accountIndex == null) ? $@"
                AND     AccountIndex IN (SELECT AccountIndex FROM HDAccount WHERE WalletId = {strWalletId})" : $@"
                AND     AccountIndex = {strAccountIndex}")} {((addressType == null) ? $@"
                AND     AddressType IN (0, 1)" : $@"
                AND     AddressType = {strAddressType}")} {((addressIndex == null) ? "" : $@"
                AND     AddressIndex = {strAddressIndex}")} {((prev == null) ? "" : (!descending ? $@"
                AND 	(OutputTxTime > {strPrevTime} OR (OutputTxTime = {strPrevTime} AND OutputIndex > {strPrevIndex}))" : $@"
                AND 	(OutputTxTime < {strPrevTime} OR (OutputTxTime = {strPrevTime} AND OutputIndex < {strPrevIndex}))"))} {(!descending ? $@"
                ORDER   BY WalletId, AccountIndex, OutputTxTime, OutputIndex" : $@"
                ORDER   BY WalletId DESC, AccountIndex DESC, OutputTxTime DESC, OutputIndex DESC")}
                LIMIT   {strLimit}");
        }

        internal static IEnumerable<HDTransactionData> GetSpendableTransactions(DBConnection conn, int walletId, int accountIndex, int currentChainHeight, long coinbaseMaturity, int confirmations = 0)
        {
            int maxConfirmationHeight = (currentChainHeight + 1) - confirmations;
            int maxCoinBaseHeight = currentChainHeight - (int)coinbaseMaturity;

            string strWalletId = DBParameter.Create(walletId);
            string strAccountIndex = DBParameter.Create(accountIndex);
            string strMaxConfirmationHeight = DBParameter.Create(maxConfirmationHeight);
            string strMaxCoinBaseHeight = DBParameter.Create(maxCoinBaseHeight);

            return conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   (WalletId, AccountIndex) IN (SELECT {strWalletId}, {strAccountIndex})
                AND     SpendTxTime IS NULL {((confirmations == 0) ? "" : $@"
                AND     OutputBlockHeight <= {strMaxConfirmationHeight}")}
                AND     (OutputTxIsCoinBase = 0 OR OutputBlockHeight <= {strMaxCoinBaseHeight})
                AND     Value > 0
                ORDER   BY OutputBlockHeight
                ,       OutputTxId
                ,       OutputIndex");
        }

        public class BalanceData
        {
            public decimal TotalBalance { get; set; }
            public decimal SpendableBalance { get; set; }
            public decimal ConfirmedBalance { get; set; }
        }

        internal static (decimal total, decimal confirmed, decimal spendable) GetBalance(DBConnection conn, int walletId, int accountIndex, (int type, int index)? address, int currentChainHeight, int coinbaseMaturity, int confirmations = 0)
        {
            int maxConfirmationHeight = (currentChainHeight + 1) - confirmations;
            int maxCoinBaseHeight = currentChainHeight - (int)coinbaseMaturity;

            string strWalletId = DBParameter.Create(walletId);
            string strAccountIndex = DBParameter.Create(accountIndex);
            string strMaxConfirmationHeight = DBParameter.Create(maxConfirmationHeight);
            string strMaxCoinBaseHeight = DBParameter.Create(maxCoinBaseHeight);

            var balanceData = conn.FindWithQuery<BalanceData>($@"
                SELECT SUM(Value) TotalBalance
                ,      SUM(CASE WHEN OutputBlockHeight <= {strMaxConfirmationHeight} AND (OutputTxIsCoinBase = 0 OR OutputBlockHeight <= {strMaxCoinBaseHeight}) THEN Value ELSE 0 END) SpendableBalance
                ,      SUM(CASE WHEN OutputBlockHeight IS NOT NULL THEN Value ELSE 0 END) ConfirmedBalance
                FROM   HDTransactionData
                WHERE  (WalletId, AccountIndex) IN (SELECT {strWalletId}, {strAccountIndex})
                AND    SpendTxTime IS NULL { ((address == null) ? "" : $@"
                AND    (AddressType, AddressIndex) IN (SELECT {DBParameter.Create(address?.type)}, {DBParameter.Create(address?.index)})")}
                AND    Value > 0");

            return (balanceData.TotalBalance, balanceData.ConfirmedBalance, balanceData.SpendableBalance);
        }

        // Finds account transactions acting as inputs to other wallet transactions - i.e. not a complete list of transaction inputs.
        internal static IEnumerable<HDTransactionData> FindTransactionInputs(DBConnection conn, int walletId, int? accountIndex, long? transactionTime, string transactionId)
        {
            string strWalletId = DBParameter.Create(walletId);
            string strAccountIndex = DBParameter.Create(accountIndex);
            string strTransactionTime = DBParameter.Create(transactionTime);
            string strTransactionId = DBParameter.Create(transactionId);

            return conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   WalletId = {strWalletId} {((accountIndex != null) ? $@"
                AND     AccountIndex = {strAccountIndex}" : $@"
                AND     AccountIndex IN (SELECT AccountIndex FROM HDAccount WHERE WalletId = {strWalletId})")} { ((transactionTime == null) ? "" : $@"
                AND     SpendTxTime = {strTransactionTime}")}
                AND     SpendTxId = {strTransactionId}");
        }

        // Finds the wallet transaction data related to a transaction - i.e. not a complete list of transaction outputs.
        internal static IEnumerable<HDTransactionData> FindTransactionOutputs(DBConnection conn, int walletId, int? accountIndex, long? transactionTime, string transactionId)
        {
            string strWalletId = DBParameter.Create(walletId);
            string strAccountIndex = DBParameter.Create(accountIndex);
            string strTransactionTime = DBParameter.Create(transactionTime);
            string strTransactionId = DBParameter.Create(transactionId);

            return conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   WalletId = {strWalletId} {((accountIndex != null) ? $@"
                AND     AccountIndex = {strAccountIndex}" : $@"
                AND     AccountIndex IN (SELECT AccountIndex FROM HDAccount WHERE WalletId = {strWalletId})")} { ((transactionTime == null) ? "" : $@"
                AND     OutputTxTime = {strTransactionTime}")}
                AND     OutputTxId = {strTransactionId}");
        }
    }
}
