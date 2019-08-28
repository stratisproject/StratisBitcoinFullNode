using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Bitcoin.Features.PoA
{
    /// <summary>
    /// Full node extension methods, from the perspective of a POA enabled node.
    /// </summary>
    public static class IFullNodeExtensions
    {
        /// <summary>
        /// Determines whether or not this node is mining using POA consensus.
        /// </summary>
        /// <param name="fullNode">The full node instance to check.</param>
        /// <returns><c>true</c> if the node has <see cref="IPoAMiner"/> in it's service collection and the mining task is running.</returns>
        public static bool IsMiningOnSideChain(this IFullNode fullNode)
        {
            IPoAMiner miner = fullNode.Services.ServiceProvider.GetService<IPoAMiner>();
            return miner == null ? false : miner.IsMining;
        }
    }
}