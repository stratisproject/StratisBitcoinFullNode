using System.Collections.Generic;
using SQLite;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class HDPayment
    {
        public int OutputTxTime { get; set; }
        public string OutputTxId { get; set; }
        public int OutputIndex { get; set; }
        public int SpendIndex { get; set; }
        public string SpendScriptPubKey { get; set; }
        public decimal SpendValue { get; set; }

        internal static IEnumerable<string> CreateScript()
        {
            yield return $@"
            CREATE TABLE HDPayment (
                OutputTxTime        INTEGER NOT NULL,
                OutputTxId          TEXT NOT NULL,
                OutputIndex         INTEGER NOT NULL,
                SpendIndex          INTEGER NOT NULL,
                SpendScriptPubKey   TEXT,
                SpendValue          DECIMAL NOT NULL,
                PRIMARY KEY(OutputTxTime, OutputTxId, OutputIndex, SpendIndex)
            )";
        }

        internal static void CreateTable(SQLiteConnection conn)
        {
            foreach (string command in CreateScript())
                conn.Execute(command);
        }

        internal static IEnumerable<HDPayment> GetAllPayments(DBConnection conn, int outputTxTime, string outputTxId, int outputIndex)
        {
            return conn.Query<HDPayment>($@"
                SELECT  *
                FROM    HDPayment
                WHERE   OutputTxTime = ?
                AND     OutputTxId = ?
                AND     OutputIndex = ?
                ORDER   BY SpendIndex",
                outputTxTime,
                outputTxId,
                outputIndex);
        }
    }
}
