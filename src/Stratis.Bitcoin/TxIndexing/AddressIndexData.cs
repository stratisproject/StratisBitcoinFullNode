using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.TxIndexing
{
    /// <summary>Contains information about operations that happened to an address.</summary>
    public class AddressIndexData : IBitcoinSerializable
    {
        public List<AddressBalanceChange> AddressBalanceChanges;

        /// <inheritdoc />
        public void ReadWrite(BitcoinStream stream)
        {
            if (stream.Serializing)
            {
                AddressBalanceChange[] arr = this.AddressBalanceChanges.ToArray();

                stream.ReadWrite(ref arr);
            }
            else
            {
                AddressBalanceChange[] arr = null;
                stream.ReadWrite(ref arr);

                this.AddressBalanceChanges = arr.ToList();
            }
        }
    }

    public class AddressBalanceChange : IBitcoinSerializable
    {
        /// <summary><c>true</c> if there was a deposit to an address, <c>false</c> if it was a withdrawal.</summary>
        public bool Deposited;

        public Money Amount;

        /// <summary>Height of a block in which operation was confirmed.</summary>
        public int Height;

        /// <inheritdoc />
        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.Deposited);

            if (stream.Serializing)
            {
                long satoshis = this.Amount.Satoshi;
                stream.ReadWrite(ref satoshis);
            }
            else
            {
                long satoshis = 0;
                stream.ReadWrite(ref satoshis);
                this.Amount = new Money(satoshis);
            }

            stream.ReadWrite(ref this.Height);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.Deposited)}:{this.Deposited}, {nameof(this.Amount)}:{this.Amount}, {nameof(this.Height)}:{this.Height}";
        }
    }
}
