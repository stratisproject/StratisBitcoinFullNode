using System;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletException : Exception
    {
        public WalletException(string message) : base(message)
        {
        }
    }
}
