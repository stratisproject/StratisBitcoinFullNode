using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// This static class is used inside smart contracts. Developers can access each field.
    /// This may be non-static and injected via constructor in the future.
    /// </summary>
    public static class Message
    {
        internal static Address ContractAddress { get; private set; }

        public static Address Sender { get; private set; }

        public static ulong GasLimit { get; private set; }

        public static ulong Value { get; private set; }

        internal static void Set(Address contractAddress, Address sender, ulong value, ulong gasLimit)
        {
            ContractAddress = contractAddress;
            Sender = sender;
            Value = value;
            GasLimit = gasLimit;
        }
    }
}
