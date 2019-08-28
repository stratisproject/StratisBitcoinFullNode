using System.Collections.Generic;
using NBitcoin;
using SQLite;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class HDAccount
    {
        public int WalletId { get; set; }
        public int AccountIndex { get; set; }
        public string AccountName { get; set; }
        public string ExtPubKey { get; set; }
        public int CreationTime { get; set; }

        private static Dictionary<string, ExtPubKey> extPubKeys = new Dictionary<string, ExtPubKey>();

        internal ExtPubKey GetExtPubKey(Network network)
        {
            if (!extPubKeys.TryGetValue(this.ExtPubKey, out ExtPubKey extPubKey))
            {
                extPubKey = NBitcoin.ExtPubKey.Parse(this.ExtPubKey, network);
                extPubKeys[this.ExtPubKey] = extPubKey;
            }

            return extPubKey;
        }

        internal static IEnumerable<string> CreateScript()
        {
            yield return $@"
            CREATE TABLE HDAccount (
                WalletId            INTEGER NOT NULL,
                AccountIndex        INTEGER NOT NULL,
                AccountName         TEXT NOT NULL,
                ExtPubKey           TEXT NOT NULL UNIQUE,
                CreationTime        INTEGER NOT NULL,
                PRIMARY KEY(WalletId, AccountIndex)
            );";

            yield return "CREATE UNIQUE INDEX UX_HDAccount_AccountName ON HDAccount(WalletId, AccountName)";
        }

        internal static HDAccount GetAccount(SQLiteConnection conn, int walletId, int accountIndex)
        {
            return conn.Find<HDAccount>(a => a.WalletId == walletId && a.AccountIndex == accountIndex);
        }

        internal static IEnumerable<HDAccount> GetAccounts(SQLiteConnection conn, int walletId)
        {
            return conn.Query<HDAccount>($@"
                    SELECT  *
                    FROM    HDAccount
                    WHERE   WalletId = {walletId}");
        }

        internal static void CreateTable(SQLiteConnection conn)
        {
            foreach (string command in CreateScript())
                conn.Execute(command);
        }
    }
}
