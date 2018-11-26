using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Test
{
    /// <summary>
    /// Wrapper for addresses when interacting with <see cref="TestChain"/>.
    /// TODO: Validation on the validity of the given string as a base 58 address.
    /// </summary>
    public struct Base58Address
    {
        public string Value { get; private set; }

        public Base58Address(string value)
        {
            this.Value = value;
        }

        public static explicit operator Base58Address(string value)
        {
            return new Base58Address(value);
        }

        public static implicit operator string(Base58Address address)
        {
            return address.Value;
        }
    }
}
