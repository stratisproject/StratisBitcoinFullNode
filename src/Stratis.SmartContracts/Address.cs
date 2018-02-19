using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// This is only really used to aid Smart Contract Developers' understanding of addresses.
    /// They may not easily understand the idea of sending to a uint160
    /// </summary>
    public class Address
    {
        private uint160 numeric;

        public Address(string address)
        {
            this.numeric = new uint160(address);
        }

        public Address(ulong numeric) : this( (uint160) numeric) { }

        public static readonly Address Zero = new Address(0);

        internal Address(uint160 numeric)
        {
            this.numeric = numeric;
        }

        public uint160 ToUint160()
        {
            return this.numeric;
        }

        public override string ToString()
        {
            return this.numeric.ToString();
        }

        public static bool operator ==(Address obj1, Address obj2)
        {
            if (ReferenceEquals(obj1, obj2))
                return true;
            else if (ReferenceEquals(obj1, null) != ReferenceEquals(obj2, null))
                return false;

            return obj1.numeric == obj2.numeric;
        }

        public static bool operator !=(Address obj1, Address obj2)
        {
            return !(obj1 == obj2);
        }

        public override bool Equals(object obj)
        {
            return this == (Address) obj;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
