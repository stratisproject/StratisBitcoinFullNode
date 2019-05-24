using System.Collections.Generic;
using LiteDB;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    public class AddressIndexerTipData
    {
        public int Id { get; set; }

        public byte[] TipHashBytes { get; set; }

        public int Height { get; set; }
    }

    public class AddressIndexerData
    {
        [BsonId]
        public string Address { get; set; }

        public List<AddressBalanceChange> BalanceChanges { get; set; }
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

    public class OutPointData
    {
        [BsonId]
        public string Outpoint { get; set; }

        public byte[] ScriptPubKeyBytes { get; set; }

        public long Money { get; set; }
    }
}
