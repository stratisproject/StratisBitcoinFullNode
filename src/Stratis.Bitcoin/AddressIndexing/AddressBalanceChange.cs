using NBitcoin;

namespace Stratis.Bitcoin.AddressIndexing
{
    public class AddressBalanceChange
    {
        /// <summary>Id required for litedb.</summary>
        public int Id { get; set; }

        /// <summary><c>true</c> if there was a deposit to an address, <c>false</c> if it was a withdrawal.</summary>
        public bool Deposited { get; set; }

        public Money Amount { get; set; }

        /// <summary>Height of a block in which operation was confirmed.</summary>
        public int Height { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.Deposited)}:{this.Deposited}, {nameof(this.Amount)}:{this.Amount}, {nameof(this.Height)}:{this.Height}";
        }
    }
}
