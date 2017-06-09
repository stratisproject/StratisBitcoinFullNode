using System;

namespace Stratis.Bitcoin.Wallet
{
    public class WalletException : Exception
    {
		public WalletException(string message) : base(message)
		{ }
	}
}
