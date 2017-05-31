using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Wallet.Helpers
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

            switch (network.ToLowerInvariant())
            {
                case "main":
                case "mainnet":
                    return Network.Main;
                case "test":
                case "testnet":
                    return Network.TestNet;
                default:
                    throw new ArgumentException($"Network '{network}' is not a valid network.");
            }
        }

    }
}
