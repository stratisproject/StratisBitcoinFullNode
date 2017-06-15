using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    public static class NetworkExtensions
    {
        /// <summary>
        /// Determines whether this network is a test network.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <returns>
        ///   <c>true</c> if the specified network is test; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsTest(this Network network)
        {
            return network.Name.ToLowerInvariant().Contains("test");
        }
    }
}
