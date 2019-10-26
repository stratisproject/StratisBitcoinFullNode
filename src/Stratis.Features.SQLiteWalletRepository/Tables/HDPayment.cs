using System.Collections.Generic;
using SQLite;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class HDPayment
    {
        public long OutputTxTime { get; set; }
        public string OutputTxId { get; set; }
        public int OutputIndex { get; set; }
        public int SpendIndex { get; set; }
        public string SpendScriptPubKey { get; set; }
        public decimal SpendValue { get; set; }
        public int SpendIsChange { get; set; }

        internal static IEnumerable<string> CreateScript()
        {
            yield return $@"
            CREATE TABLE HDPayment (
                SpendTxTime         INTEGER NOT NULL,
                SpendTxId           TEXT NOT NULL,
                OutputTxId          TEXT NOT NULL,
                OutputIndex         INTEGER NOT NULL,
                ScriptPubKey        TEXT NOT NULL,
                SpendIndex          INTEGER NOT NULL,
                SpendScriptPubKey   TEXT,
                SpendValue          DECIMAL NOT NULL,
                SpendIsChange       INTEGER NOT NULL,
                PRIMARY KEY(SpendTxTime, SpendTxId, OutputTxId, OutputIndex, ScriptPubKey, SpendIndex)
            )";
        }

        internal static void CreateTable(SQLiteConnection conn)
        {
            foreach (string command in CreateScript())
                conn.Execute(command);
        }

        internal static IEnumerable<HDPayment> GetAllPayments(DBConnection conn, long spendTxTime, string spendTxId, string outputTxId, int outputIndex, string scriptPubKey)
        {
            return conn.Query<HDPayment>($@"
                SELECT  *
                FROM    HDPayment
                WHERE   SpendTxTime = ?
                AND     SpendTxID = ?
                AND     OutputTxId = ?
                AND     OutputIndex = ?
                AND     ScriptPubKey = ?
                ORDER   BY SpendIndex",
                spendTxTime,
                spendTxId,
                outputTxId,
                outputIndex,
                scriptPubKey);
        }
    }
}
