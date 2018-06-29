using System;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Contains a collection of helpers methods.
    /// </summary>
    public static class NetworkHelpers
    {
        /// <summary>
        /// Get the network on which to operate.
        /// </summary>
        /// <param name="network">The network</param>
        /// <returns>A <see cref="Network"/> object.</returns>
        public static Network GetNetwork(string network)
        {
            Guard.NotEmpty(network, nameof(network));

            Network selectNetwork = NetworksContainer.GetNetwork(network.ToLowerInvariant());

            if (selectNetwork == null)
                throw new ArgumentException($"Network '{network}' is not a valid network.");

            return selectNetwork;
        }
    }
}
