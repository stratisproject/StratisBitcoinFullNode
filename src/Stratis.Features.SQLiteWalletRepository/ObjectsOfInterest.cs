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
        private HashSet<byte[]> tentative;

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

        protected bool Contains(byte[] obj)
        {
            if (this.tentative.Contains(obj))
                return true;

            if (!this.MayContain(obj))
                return false;

            return this.Exists(obj);
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

        protected virtual bool Exists(byte[] obj)
        {
            return false;
        }

        public void Confirm()
        {
            foreach (byte[] obj in this.tentative)
            {
                if (this.Exists(obj))
                    Add(obj);

                this.Add(obj);
            }

            this.tentative.Clear();
        }
    }

    internal class AddressesOfInterest : ObjectsOfInterest
    {
        private readonly DBConnection conn;
        private readonly int? walletId;

        public AddressesOfInterest(DBConnection conn, int? walletId)
        {
            this.conn = conn;
            this.walletId = walletId;
        }

        public bool Contains(Script scriptPubKey)
        {
            return Contains(scriptPubKey.ToBytes());
        }

        protected void Add(Script scriptPubKey)
        {
            this.Add(scriptPubKey.ToBytes());
        }

        public void AddTentative(Script scriptPubKey)
        {
            this.AddTentative(scriptPubKey.ToBytes());
        }

        public void AddAll()
        {
            Clear();

            List<HDAddress> addresses = this.conn.Query<HDAddress>($@"
                SELECT  *
                FROM    HDAddress {
            // Restrict to wallet if provided.
            ((this.walletId != null) ? $@"
                WHERE   WalletId = {this.walletId}" : "")}");

            foreach (HDAddress address in addresses)
            {
                this.Add(Script.FromHex(address.ScriptPubKey));
                //this.Add(Script.FromHex(address.PubKey));
            }
        }

        protected override bool Exists(byte[] obj)
        {
            var scriptPubKey = new Script(obj);

            string hex = scriptPubKey.ToHex();

            bool res = this.conn.ExecuteScalar<int>($@"
                        SELECT EXISTS(
                            SELECT  1
                            FROM    HDAddress
                            WHERE   ScriptPubKey = ? {
                    // Restrict to wallet if provided.
                    ((this.walletId != null) ? $@"
                            AND     WalletId = {this.walletId}" : "")}
                            LIMIT   1);", hex) == 1;

            return res;
        }
    }

    internal class TransactionsOfInterest : ObjectsOfInterest
    {
        private readonly DBConnection conn;
        private readonly int? walletId;

        public TransactionsOfInterest(DBConnection conn, int? walletId)
        {
            this.conn = conn;
            this.walletId = walletId;
        }

        public bool Contains(uint256 txId)
        {
            return Contains(txId.ToBytes());
        }

        protected void Add(uint256 txId)
        {
            this.Add(txId.ToBytes());
        }

        public void AddTentative(uint256 txId)
        {
            this.AddTentative(txId.ToBytes());
        }

        public void AddAll()
        {
            Clear();

            List<HDTransactionData> spendableTransactions = this.conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   SpendBlockHash IS NULL
                AND     SpendBlockHeight IS NULL {
                // Restrict to wallet if provided.
                ((this.walletId != null) ? $@"
                AND      WalletId = {this.walletId}" : "")}");

            foreach (HDTransactionData transactionData in spendableTransactions)
                this.Add(uint256.Parse(transactionData.OutputTxId).ToBytes());
        }

        protected override bool Exists(byte[] obj)
        {
            var txId = new uint256(obj);

            bool res = this.conn.ExecuteScalar<int>($@"
                SELECT EXISTS(
                    SELECT  1
                    FROM    HDTransactionData
                    WHERE   OutputTxId = ? {
                // Restrict to wallet if provided.
                ((this.walletId != null) ? $@"
                    AND     WalletId = {this.walletId}" : "")}
                    LIMIT   1);", txId.ToString()) == 1;

            return res;
        }
    }
}
