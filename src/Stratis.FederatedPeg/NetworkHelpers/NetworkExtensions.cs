using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg
{
    /// <summary>
    /// Network helper extensions for identifying a sidechain or mainchain network.
    /// </summary>
    public static class NetworkExtensions
    {
        public static readonly List<string> MainChainNames = new List<Network> {
            Network.StratisMain, Network.StratisTest, Network.StratisRegTest,
            Network.Main, Network.TestNet, Network.RegTest
        }.Select(n => n.Name.ToLower()).ToList();

        /// <summary>
        /// Returns whether we are on a sidechain or a mainchain network.
        /// </summary>
        /// <param name="network">The network to examine.</param>
        /// <returns>This function tests for a sidechain and returns mainchain for any non sidechain network.</returns>
        public static Chain ToChain(this Network network)
        {
            return MainChainNames.Contains(network.Name.ToLower()) ? Chain.Mainchain : Chain.Sidechain;
        }

        /// <summary>
        /// Returns the network's counter chain network.
        /// </summary>
        /// <param name="network">The network to examine.</param>
        /// <returns></returns>
        public static Network ToCounterChainNetwork(this Network network)
        {
            if (network == Network.StratisMain) return ApexNetwork.Main;
            if (network == Network.StratisTest) return ApexNetwork.Test;
            if (network == Network.StratisRegTest) return ApexNetwork.RegTest;
            if (network == ApexNetwork.Main) return Network.StratisMain;
            if (network == ApexNetwork.Test) return Network.StratisTest;
            if (network == ApexNetwork.RegTest) return Network.StratisRegTest;
            throw new System.ArgumentException("Unknown network.");
        }
    }
}