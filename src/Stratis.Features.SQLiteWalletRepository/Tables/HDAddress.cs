using System.Collections.Generic;
using SQLite;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class HDAddress
    {
        // AddressType constants.
        public const int External = 0;
        public const int Internal = 1;

        public int WalletId { get; set; }
        public int AccountIndex { get; set; }
        public int AddressType { get; set; }
        public int AddressIndex { get; set; }
        public string ScriptPubKey { get; set; }
        public string PubKey { get; set; }

        internal static IEnumerable<string> CreateScript()
        {
            yield return $@"
            CREATE TABLE HDAddress (
                WalletId            INTEGER NOT NULL,
                AccountIndex        INTEGER NOT NULL,
                AddressType         INTEGER NOT NULL,
                AddressIndex        INTEGER NOT NULL,
                ScriptPubKey        TEXT    NOT NULL,
                PubKey              TEXT,
                PRIMARY KEY(WalletId, AccountIndex, AddressType, AddressIndex)
            )";

            yield return "CREATE UNIQUE INDEX UX_HDAddress_ScriptPubKey ON HDAddress(WalletId, ScriptPubKey)";
            yield return "CREATE UNIQUE INDEX UX_HDAddress_PubKey ON HDAddress(WalletId, PubKey)";
        }

        internal static void CreateTable(SQLiteConnection conn)
        {
            foreach (string command in CreateScript())
                conn.Execute(command);
        }

        internal static IEnumerable<HDAddress> GetAccountAddresses(SQLiteConnection conn, int walletId, int accountIndex, int addressType, int count)
        {
            return conn.Query<HDAddress>($@"
                SELECT  A.*
                FROM    HDAddress A
                LEFT    JOIN HDTransactionData D
                ON      D.WalletId = A.WalletId
                AND     D.AccountIndex = A.AccountIndex
                AND     D.AddressType = A.AddressType
                AND     D.AddressIndex = A.AddressIndex
                WHERE   A.WalletId = ?
                AND     A.AccountIndex = ?
                AND     A.AddressType = ?
                GROUP   BY A.WalletId, A.AccountIndex, A.AddressType, A.AddressIndex
                ORDER   BY AddressType, AccountIndex
                LIMIT   ?;",
                walletId,
                accountIndex,
                addressType,
                count);
        }

        internal static IEnumerable<HDAddress> GetUsedAddresses(SQLiteConnection conn, int walletId, int accountIndex, int addressType, int count)
        {
            return conn.Query<HDAddress>($@"
                SELECT  A.*
                FROM    HDAddress A
                LEFT    JOIN HDTransactionData D
                ON      D.WalletId = A.WalletId
                AND     D.AccountIndex = A.AccountIndex
                AND     D.AddressType = A.AddressType
                AND     D.AddressIndex = A.AddressIndex
                WHERE   A.WalletId = ?
                AND     A.AccountIndex = ?
                AND     A.AddressType = ?
                GROUP   BY A.WalletId, A.AccountIndex, A.AddressType, A.AddressIndex
                HAVING  MAX(D.WalletId) IS NOT NULL
                ORDER   BY AddressType, AccountIndex
                LIMIT   ?;",
                walletId,
                accountIndex,
                addressType,
                count);
        }

        internal static IEnumerable<HDAddress> GetUnusedAddresses(SQLiteConnection conn, int walletId, int accountIndex, int addressType, int count)
        {
            return conn.Query<HDAddress>($@"
                SELECT  A.*
                FROM    HDAddress A
                LEFT    JOIN HDTransactionData D
                ON      D.WalletId = A.WalletId
                AND     D.AccountIndex = A.AccountIndex
                AND     D.AddressType = A.AddressType
                AND     D.AddressIndex = A.AddressIndex
                WHERE   A.WalletId = ?
                AND     A.AccountIndex = ?
                AND     A.AddressType = ?
                GROUP   BY A.WalletId, A.AccountIndex, A.AddressType, A.AddressIndex
                HAVING  MAX(D.WalletId) IS NULL
                ORDER   BY AddressType, AccountIndex
                LIMIT   ?;",
                walletId,
                accountIndex,
                addressType,
                count);
        }

        internal static HDAddress GetAddress(SQLiteConnection conn, int walletId, int accountIndex, int addressType, int addressIndex)
        {
            return conn.FindWithQuery<HDAddress>($@"
                SELECT  *
                FROM    HDAddress
                WHERE   WalletId = {walletId}
                AND     AccountIndex = {accountIndex}
                AND     AddressType = {addressType}
                AND     AddressIndex = {addressIndex}");
        }

        internal static int GetAddressCount(SQLiteConnection conn, int walletId, int accountIndex, int addressType)
        {
            return 1 + (conn.ExecuteScalar<int?>($@"
                SELECT  MAX(AddressIndex)
                FROM    HDAddress
                WHERE   WalletId = {walletId}
                AND     AccountIndex = {accountIndex}
                AND     AddressType = {addressType}") ?? -1);
        }

        internal static int GetNextAddressIndex(SQLiteConnection conn, int walletId, int accountIndex, int addressType)
        {
            return 1 + (conn.ExecuteScalar<int?>($@"
                SELECT  MAX(AddressIndex)
                FROM    HDTransactionData
                WHERE   WalletId = ?
                AND     AccountIndex = ?
                AND     AddressType = ?",
                walletId, accountIndex, addressType) ?? -1);
        }
    }
}
