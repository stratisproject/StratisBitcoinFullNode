using System;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class CannotAddAccountToXpubKeyWalletException : Exception
    {
        public CannotAddAccountToXpubKeyWalletException(string message) : base(message)
        {
        }
    }
}