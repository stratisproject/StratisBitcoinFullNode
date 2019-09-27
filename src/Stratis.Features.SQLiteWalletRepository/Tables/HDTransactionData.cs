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
            return conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   WalletId = {walletId} {((accountIndex == null) ? $@"
                AND     AccountIndex IN (SELECT AccountIndex FROM HDAccount WHERE WalletId = {walletId})" : $@"
                AND     AccountIndex = {accountIndex}")} {((addressType == null) ? $@"
                AND     AddressType IN (0, 1)" : $@"
                AND     AddressType = {addressType}")} {((addressIndex == null) ? "" : $@"
                AND     AddressIndex = {addressIndex}")} {((prev == null) ? "" : (!descending ? $@"
                AND 	(OutputTxTime > {prev.OutputTxTime} OR (OutputTxTime = {prev.OutputTxTime} AND OutputIndex > {prev.OutputIndex}))" : $@"
                AND 	(OutputTxTime < {prev.OutputTxTime} OR (OutputTxTime = {prev.OutputTxTime} AND OutputIndex < {prev.OutputIndex}))"))} {(!descending ? $@"
                ORDER   BY WalletId, AccountIndex, OutputTxTime, OutputIndex" : $@"
                ORDER   BY WalletId DESC, AccountIndex DESC, OutputTxTime DESC, OutputIndex DESC")}
                LIMIT   {limit}");
        }

        internal static IEnumerable<HDTransactionData> GetSpendableTransactions(DBConnection conn, int walletId, int accountIndex, int currentChainHeight, long coinbaseMaturity, int confirmations = 0)
        {
            int maxConfirmationHeight = (currentChainHeight + 1) - confirmations;
            int maxCoinBaseHeight = currentChainHeight - (int)coinbaseMaturity;

            return conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   (WalletId, AccountIndex) IN (SELECT {walletId}, {accountIndex})
                AND     SpendTxTime IS NULL
                AND     OutputBlockHeight <= {maxConfirmationHeight}
                AND     (OutputTxIsCoinBase = 0 OR OutputBlockHeight <= {maxCoinBaseHeight})
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

            var balanceData = conn.FindWithQuery<BalanceData>($@"
                SELECT SUM(Value) TotalBalance
                ,      SUM(CASE WHEN OutputBlockHeight <= {maxConfirmationHeight} AND (OutputTxIsCoinBase = 0 OR OutputBlockHeight <= {maxCoinBaseHeight}) THEN Value ELSE 0 END) SpendableBalance
                ,      SUM(CASE WHEN OutputBlockHeight IS NOT NULL THEN Value ELSE 0 END) ConfirmedBalance
                FROM   HDTransactionData
                WHERE  (WalletId, AccountIndex) IN (SELECT {walletId}, {accountIndex})
                AND    SpendTxTime IS NULL { ((address == null) ? "" : $@"
                AND    (AddressType, AddressIndex) IN (SELECT {address?.type}, {address?.index}")}");

            return (balanceData.TotalBalance, balanceData.ConfirmedBalance, balanceData.SpendableBalance);
        }

        // Finds account transactions acting as inputs to other wallet transactions - i.e. not a complete list of transaction inputs.
        internal static IEnumerable<HDTransactionData> FindTransactionInputs(DBConnection conn, int walletId, long? transactionTime, string transactionId)
        {
            return conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   WalletId = {walletId}
                AND     AccountIndex IN (SELECT AccountIndex FROM HDAccount WHERE WalletId = {walletId}) { ((transactionTime == null && transactionId == null) ? "" : $@"
                AND     SpendTxTime = {transactionTime}
                AND     SpendTxId = '{transactionId}'")}");
        }

        // Finds the wallet transaction data related to a transaction - i.e. not a complete list of transaction outputs.
        internal static IEnumerable<HDTransactionData> FindTransactionOutputs(DBConnection conn, int walletId, int transactionTime, string transactionId)
        {
            return conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   WalletId = {walletId}
                AND     AccountIndex IN (SELECT AccountIndex FROM HDAccount WHERE WalletId = {walletId})
                AND     OutputTxTime = {transactionTime}
                AND     OutputTxId = '{transactionId}'");
        }
    }
}
