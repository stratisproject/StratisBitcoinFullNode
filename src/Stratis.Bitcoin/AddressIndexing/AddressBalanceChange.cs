using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.AddressIndexing
{
    public class AddressIndexData
    {
        /// <summary>Id required for litedb.</summary>
        public int Id { get; set; }

        public byte[] ScriptPubKeyBytes { get; set; }

        public List<AddressBalanceChange> Changes { get; set; }
    }

    public class AddressBalanceChange
    {
        /// <summary><c>true</c> if there was a deposit to an address, <c>false</c> if it was a withdrawal.</summary>
        public bool Deposited { get; set; }

        public long Satoshi { get; set; }

        /// <summary>Height of a block in which operation was confirmed.</summary>
        public int Height { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.Deposited)}:{this.Deposited}, {nameof(this.Satoshi)}:{this.Satoshi}, {nameof(this.Height)}:{this.Height}";
        }
    }
}
