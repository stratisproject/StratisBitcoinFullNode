using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts
{
    // This is only really used to aid Smart Contract Developers' understanding of addresses

    // They may not easily understand the idea of sending to a uint160

    public class Address
    {
        private uint160 _numeric;

        public Address(string address)
        {
            throw new NotImplementedException("Need to convert the string to a numeric representation");
        }

        public Address(uint160 numeric)
        {
            _numeric = numeric;
        }

        public uint160 ToUint160()
        {
            return _numeric;
        }
    }
}
