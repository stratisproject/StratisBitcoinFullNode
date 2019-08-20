using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    internal class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public int GetHashCode(byte[] obj)
        {
            ulong hash = 17;

            foreach (byte objByte in obj)
            {
                hash = (hash << 5) - hash + objByte;
            }

            return (int)hash;
        }

        public bool Equals(byte[] obj1, byte[] obj2)
        {
            if (obj1.Length != obj2.Length)
                return false;

            for (int i = 0; i < obj1.Length; i++)
                if (obj1[i] != obj2[i])
                    return false;

            return true;
        }
    }

    internal class ObjectsOfInterest
    {
        private byte[] hashArray;
        private int maxHashArrayLengthLog;
        private uint bitIndexLimiter;
        protected HashSet<byte[]> tentative;

        public ObjectsOfInterest(int MaxHashArrayLengthLog = 26)
        {
            this.maxHashArrayLengthLog = MaxHashArrayLengthLog;
            this.bitIndexLimiter = ((uint)1 << (this.maxHashArrayLengthLog + 3)) - 1;
            this.tentative = new HashSet<byte[]>(new ByteArrayEqualityComparer());

            this.Clear();
        }

        public void Clear()
        {
            this.hashArray = new byte[1 << this.maxHashArrayLengthLog];
            this.tentative.Clear();
        }

        private uint GetHashCode(byte[] obj)
        {
            ulong hash = 17;

            foreach (byte objByte in obj)
            {
                hash = (hash << 5) - hash + objByte;
            }

            return (uint)hash;
        }

        protected bool MayContain(byte[] obj)
        {
            uint hashArrayBitIndex = this.GetHashCode(obj) & this.bitIndexLimiter;

            return (this.hashArray[hashArrayBitIndex >> 3] & (1 << (int)(hashArrayBitIndex & 7))) != 0;
        }

        protected bool? Contains(byte[] obj)
        {
            if (this.tentative.Contains(obj))
                return true;

            if (!this.MayContain(obj))
                return false;

            // May contain...
            return null;
        }

        protected void Add(byte[] obj)
        {
            uint hashArrayBitIndex = this.GetHashCode(obj) & this.bitIndexLimiter;

            this.hashArray[hashArrayBitIndex >> 3] |= (byte)(1 << (int)(hashArrayBitIndex & 7));
        }

        protected void AddTentative(byte[] obj)
        {
            if (!this.MayContain(obj))
                this.tentative.Add(obj);
        }

        public void Confirm(Func<byte[], bool> exists)
        {
            foreach (byte[] obj in this.tentative)
                if (exists(obj))
                    Add(obj);

            this.tentative.Clear();
        }
    }

    internal class AddressesOfInterest : ObjectsOfInterest
    {
        public bool Contains(Script scriptPubKey, DBConnection conn, int? walletId)
        {
            return Contains(scriptPubKey.ToBytes()) ?? Exists(conn, scriptPubKey, walletId);
        }

        public void Confirm(DBConnection conn, int? walletId)
        {
            Confirm(o => this.Exists(conn, new Script(o), walletId));
        }

        protected void Add(Script scriptPubKey)
        {
            this.Add(scriptPubKey.ToBytes());
        }

        public void AddTentative(Script scriptPubKey)
        {
            this.AddTentative(scriptPubKey.ToBytes());
        }

        public void AddAll(DBConnection conn, int? walletId)
        {
            Clear();

            List<HDAddress> addresses = conn.Query<HDAddress>($@"
                SELECT  *
                FROM    HDAddress {
            // Restrict to wallet if provided.
            ((walletId != null) ? $@"
                WHERE   WalletId = {walletId}" : "")}");

            foreach (HDAddress address in addresses)
            {
                this.Add(Script.FromHex(address.ScriptPubKey));
            }
        }

        internal bool Exists(DBConnection conn, Script scriptPubKey, int? walletId)
        {
            string hex = scriptPubKey.ToHex();

            bool res = conn.ExecuteScalar<int>($@"
                        SELECT EXISTS(
                            SELECT  1
                            FROM    HDAddress
                            WHERE   ScriptPubKey = ? {
                    // Restrict to wallet if provided.
                    ((walletId != null) ? $@"
                            AND     WalletId = {walletId}" : "")}
                            LIMIT   1);", hex) == 1;

            return res;
        }
    }

    internal class TransactionsOfInterest : ObjectsOfInterest
    {
        public bool Contains(uint256 txId, DBConnection conn, int? walletId)
        {
            return Contains(txId.ToBytes()) ?? Exists(conn, txId, walletId);
        }

        public void Confirm(DBConnection conn, int? walletId)
        {
            Confirm(o => this.Exists(conn, new uint256(o), walletId));
        }

        protected void Add(uint256 txId)
        {
            this.Add(txId.ToBytes());
        }

        public void AddTentative(uint256 txId)
        {
            this.AddTentative(txId.ToBytes());
        }

        public void AddAll(DBConnection conn, int? walletId)
        {
            Clear();

            List<HDTransactionData> spendableTransactions = conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   SpendBlockHash IS NULL
                AND     SpendBlockHeight IS NULL {
                // Restrict to wallet if provided.
                ((walletId != null) ? $@"
                AND      WalletId = {walletId}" : "")}");

            foreach (HDTransactionData transactionData in spendableTransactions)
                this.Add(uint256.Parse(transactionData.OutputTxId).ToBytes());
        }

        internal bool Exists(DBConnection conn, uint256 txId, int? walletId)
        {
            bool res = conn.ExecuteScalar<int>($@"
                SELECT EXISTS(
                    SELECT  1
                    FROM    HDTransactionData
                    WHERE   OutputTxId = ? {
                // Restrict to wallet if provided.
                ((walletId != null) ? $@"
                    AND     WalletId = {walletId}" : "")}
                    LIMIT   1);", txId.ToString()) == 1;

            return res;
        }
    }
}
