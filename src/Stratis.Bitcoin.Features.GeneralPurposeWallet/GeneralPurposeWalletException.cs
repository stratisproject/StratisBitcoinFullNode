using System;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet
{
    public class GeneralPurposeWalletException : Exception
    {
        public GeneralPurposeWalletException(string message) : base(message)
        {
        }
    }
}
