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

        /// <summary>Default minimum transaction fee for bitcoin network.</summary>
        private const long DefaultBitcoinMinTxFee = 1000;

        /// <summary>Default fallback fee for bitcoin network.</summary>
        private const long DefaultBitcoinFallbackTxFee = 20000;

        /// <summary>Default minimum transaction fee for stratis network.</summary>
        private const long DefaultStratisMinTxFee = 10000;

        /// <summary>Default fallback fee for stratis network.</summary>
        private const long DefaultStratisFallbackTxFee = 40000;

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
        /// Determines whether this network is a bitcoin network.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <returns><c>true</c> if the specified network is bitcoin, <c>false</c> otherwise.</returns>
        public static bool IsBitcoin(this Network network)
        {
           return !network.Name.ToLowerInvariant().Contains("stratis");
        }

        /// <summary>
        /// The default minimum transaction fee for the network.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <returns>The default minimum transaction fee.</returns>
        public static long DefaultMinTxFee(this Network network)
        {
            return network.IsBitcoin() ? DefaultBitcoinMinTxFee : DefaultStratisMinTxFee;
        }

        /// <summary>
        /// The default fall back transaction fee for the network.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <returns>The default fallback transaction fee.</returns>
        public static long DefaultFallbackTxFee(this Network network)
        {
            return network.IsBitcoin() ? DefaultBitcoinFallbackTxFee : DefaultStratisFallbackTxFee;
        }
    }
}
