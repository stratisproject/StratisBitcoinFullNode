using System;

namespace Stratis.Features.FederatedPeg.Exceptions
{
    /// <summary>
    /// This is exception is thrown when the federation wallet tip is not found on chain.
    /// </summary>
    public sealed class FederationWalletTipNotOnChainException : Exception
    {
    }
}
