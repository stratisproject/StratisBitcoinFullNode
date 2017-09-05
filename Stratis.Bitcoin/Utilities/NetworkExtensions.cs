using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Extension methods for NBitcoin's Network class.
    /// </summary>
    public static class NetworkExtensions
    {
        /// <summary>
        /// Determines whether this network is a test network.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <returns><c>true</c> if the specified network is test, <c>false</c> otherwise.</returns>
        public static bool IsTest(this Network network)
        {
            return network.Name.ToLowerInvariant().Contains("test");
        }
    }
}
