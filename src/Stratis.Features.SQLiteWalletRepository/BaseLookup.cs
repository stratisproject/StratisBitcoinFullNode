using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

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

    internal class BaseLookup
    {
        private byte[] hashArray;
        private int maxHashArrayLengthLog;
        private uint bitIndexLimiter;
        protected Dictionary<byte[], HashSet<AddressIdentifier>> tentative;

        public BaseLookup(int MaxHashArrayLengthLog)
        {
            this.maxHashArrayLengthLog = MaxHashArrayLengthLog;
            this.hashArray = new byte[1 << this.maxHashArrayLengthLog];
            this.bitIndexLimiter = ((uint)1 << (this.maxHashArrayLengthLog + 3)) - 1;
            this.tentative = new Dictionary<byte[], HashSet<AddressIdentifier>>(new ByteArrayEqualityComparer());
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

        protected bool? Contains(byte[] obj, out HashSet<AddressIdentifier> objData)
        {
            if (this.tentative.ContainsKey(obj))
            {
                objData = this.tentative[obj];
                return true;
            }

            objData = null;

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

        protected void AddTentative(byte[] obj, AddressIdentifier address)
        {
            if (!this.tentative.TryGetValue(obj, out HashSet<AddressIdentifier> addresses))
            {
                addresses = new HashSet<AddressIdentifier>();
                this.tentative[obj] = addresses;
            }

            addresses.Add(address);
        }

        public IEnumerable<AddressIdentifier> GetTentative()
        {
            return this.tentative.SelectMany(t => t.Value);
        }

        public void Confirm(Func<byte[], bool> exists)
        {
            foreach (byte[] obj in this.tentative.Keys)
                if (exists(obj))
                    Add(obj);

            this.tentative.Clear();
        }
    }
}
