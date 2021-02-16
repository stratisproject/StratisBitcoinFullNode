using System;

namespace LedgerWallet.HIDProviders
{
    public class HIDDeviceException : Exception
    {
        public HIDDeviceException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}
