using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Extension methods for NBitcoin's Network class.
    /// </summary>
    public static class NetworkExtensions
    {
        /// <summary>Fake height value used in Coins to signify they are only in the memory pool (since 0.8).</summary>
        public const int MempoolHeight = 0x7FFFFFFF;

        /// <summary>
        /// Determines whether this network is a test network.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <returns><c>true</c> if the specified network is test, <c>false</c> otherwise.</returns>
        public static bool IsTest(this Network network)
        {
            return network.Name.ToLowerInvariant().Contains("test");
        }

        /// <summary>
        /// Determines whether this network is a regtest network.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <returns><c>true</c> if the specified network is test, <c>false</c> otherwise.</returns>
        public static bool IsRegTest(this Network network)
        {
            return network.Name.ToLowerInvariant().Contains("regtest");
        }

        /// <summary>
        /// Determines whether this network is a bitcoin network.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <returns><c>true</c> if the specified network is bitcoin, <c>false</c> otherwise.</returns>
        public static bool IsBitcoin(this Network network)
        {
            return !network.Name.ToLowerInvariant().Contains("stratis");
        }
    }
}
