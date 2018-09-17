using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers
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

        public static Chain ToChain(this Network network)
        {
            return MainChainNames.Contains(network.Name.ToLower()) ? Chain.Mainchain : Chain.Sidechain;
        }

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