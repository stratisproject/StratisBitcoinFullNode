using NBitcoin;

namespace Stratis.FederatedPeg
{
    /// <summary>
    /// Network helper extensions for identifying a sidechain or mainchain network.
    /// </summary>
    public static class NetworkExtensions
    {
        /// <summary>
        /// Returns whether we are on a sidechain or a mainchain network.
        /// </summary>
        /// <param name="network">The network to examine.</param>
        /// <returns>This function tests for a sidechain and returns mainchain for any non sidechain network.</returns>
        public static Chain ToChain(this Network network)
        {
            return network.Name.ToLower().Contains("sidechain") ? Chain.Sidechain : Chain.Mainchain;
        }
    }
}