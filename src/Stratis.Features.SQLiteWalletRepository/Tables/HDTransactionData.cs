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
        public int OutputTxTime { get; set; }
        public string OutputTxId { get; set; }
        public int OutputIndex { get; set; }
        public int OutputBlockHeight { get; set; }
        public string OutputBlockHash { get; set; }
        public int OutputTxIsCoinBase { get; set; }
        public int? SpendTxTime { get; set; }
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
                OutputBlockHash     INTEGER,
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

            yield return "CREATE UNIQUE INDEX UX_HDTransactionData_Output ON HDTransactionData(OutputTxId, OutputIndex, ScriptPubKey)";
            yield return "CREATE INDEX IX_HDTransactionData_SpendTxTime ON HDTransactionData (WalletId, AccountIndex, SpendTxTime DESC)";
            yield return "CREATE INDEX IX_HDTransactionData_OutputTxTime ON HDTransactionData (WalletId, AccountIndex, OutputTxTime DESC)";
        }

        internal static void CreateTable(SQLiteConnection conn)
        {
            foreach (string command in CreateScript())
                conn.Execute(command);
        }

        internal static IEnumerable<HDTransactionData> GetAllTransactions(DBConnection conn, int walletId, int accountIndex, int addressType, int addressIndex)
        {
            return conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   WalletId = {walletId}
                AND     AccountIndex = {accountIndex}
                AND     AddressType = {addressType}
                AND     AddressIndex = {addressIndex}
                ORDER   BY OutputTxTime");
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
            public decimal ConfirmedBalance { get; set; }
        }

        internal static (decimal total, decimal confirmed) GetBalance(DBConnection conn, int walletId, int accountIndex, (int type, int index)? address, int currentChainHeight, int coinbaseMaturity, int confirmations = 0)
        {
            int maxConfirmationHeight = (currentChainHeight + 1) - confirmations;
            int maxCoinBaseHeight = currentChainHeight - (int)coinbaseMaturity;

            var balanceData = conn.FindWithQuery<BalanceData>($@"
                SELECT SUM(Value) TotalBalance
                ,      SUM(CASE WHEN OutputBlockHeight <= {maxConfirmationHeight} AND (OutputTxIsCoinBase = 0 OR OutputBlockHeight <= {maxCoinBaseHeight}) THEN Value ELSE 0 END) ConfirmedBalance
                FROM   HDTransactionData
                WHERE  (WalletId, AccountIndex) IN (SELECT {walletId}, {accountIndex})
                AND    SpendTxTime IS NULL { ((address == null) ? "" : $@"
                AND    (AddressType, AddressIndex) IN (SELECT {address?.type}, {address?.index}")}");

            return (balanceData.TotalBalance, balanceData.ConfirmedBalance);
        }
    }
}
