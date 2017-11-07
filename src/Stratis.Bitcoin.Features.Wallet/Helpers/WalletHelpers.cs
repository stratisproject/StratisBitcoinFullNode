using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet.Helpers
{
    /// <summary>
    /// Contains a collection of helpers methods.
    /// </summary>
    public static class WalletHelpers
    {
        /// <summary>
        /// Get the network on which to operate.
        /// </summary>
        /// <param name="network">The network</param>
        /// <returns>A <see cref="Network"/> object.</returns>
        public static Network GetNetwork(string network)
        {
            Guard.NotEmpty(network, nameof(network));

            Network selectNetwork = Network.GetNetwork(network.ToLowerInvariant());

            if (selectNetwork == null)
                throw new ArgumentException($"Network '{network}' is not a valid network.");

            return selectNetwork;
        }
    }
}