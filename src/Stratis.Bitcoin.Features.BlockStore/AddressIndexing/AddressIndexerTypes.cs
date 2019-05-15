using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    public class AddressIndexerData
    {
        /// <summary>Id required for litedb.</summary>
        public int Id { get; set; }

        public byte[] TipHashBytes { get; set; }

        /// <summary>Address changes by address.</summary>
        public Dictionary<string, List<AddressBalanceChange>> AddressChanges { get; set; }
    }

    public class AddressBalanceChange
    {
        /// <summary><c>true</c> if there was a deposit to an address, <c>false</c> if it was a withdrawal.</summary>
        public bool Deposited { get; set; }

        public long Satoshi { get; set; }

        /// <summary>Height of a block in which operation was confirmed.</summary>
        public int BalanceChangedHeight { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.Deposited)}:{this.Deposited}, {nameof(this.Satoshi)}:{this.Satoshi}, {nameof(this.BalanceChangedHeight)}:{this.BalanceChangedHeight}";
        }
    }

    public class OutputsIndexData
    {
        public OutputsIndexData()
        {
            this.IndexedOutpoints = new Dictionary<OutPointModel, Tuple<byte[], long>>();
        }

        /// <summary>Id required for litedb.</summary>
        public int Id { get; set; }

        /// <summary>Script pub key bytes and amounts mapped by outpoints.</summary>
        public Dictionary<OutPointModel, Tuple<byte[], long>> IndexedOutpoints { get; set; }
    }

    public class OutPointModel
    {
        public uint256 Hash { get; set; }
        public uint N { get; set; }

        public OutPointModel()
        {
            this.Hash = uint256.Zero;
            this.N = uint.MaxValue;
        }

        public OutPointModel(OutPoint outPoint)
        {
            this.Hash = outPoint.Hash;
            this.N = outPoint.N;
        }

        public OutPointModel(uint256 hashIn, uint nIn)
        {
            this.Hash = hashIn;
            this.N = nIn;
        }

        public OutPointModel(uint256 hashIn, int nIn)
        {
            this.Hash = hashIn;
            this.N = (uint)nIn;
        }

        public OutPointModel(Transaction tx, uint i)
            : this(tx.GetHash(), i)
        {
        }

        public OutPointModel(Transaction tx, int i)
            : this(tx.GetHash(), i)
        {
        }

        public static bool operator ==(OutPointModel a, OutPointModel b)
        {
            if (ReferenceEquals(a, null))
            {
                return ReferenceEquals(b, null);
            }
            if (ReferenceEquals(b, null))
            {
                return false;
            }
            return (a.Hash == b.Hash && a.N == b.N);
        }

        public static bool operator !=(OutPointModel a, OutPointModel b)
        {
            return !(a == b);
        }
        public override bool Equals(object obj)
        {
            var item = obj as OutPointModel;
            if (ReferenceEquals(null, item))
                return false;
            return item == this;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 + this.Hash.GetHashCode() * 31 + this.N.GetHashCode() * 31 * 31;
            }
        }

        public override string ToString()
        {
            return this.Hash + "-" + this.N;
        }
    }
}
