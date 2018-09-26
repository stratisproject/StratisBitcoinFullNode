using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers
{
    /// <summary>
    /// Network helper extensions for identifying a sidechain or mainchain network.
    /// </summary>
    public static class NetworkExtensions
    {
        public static readonly List<string> MainChainNames = new List<Network> {
            new StratisMain(), new StratisTest(), new StratisRegTest(),
            new BitcoinMain(), new BitcoinTest(), new BitcoinRegTest()
        }.Select(n => n.Name.ToLower()).ToList();

        public static Chain ToChain(this Network network)
        {
            return MainChainNames.Contains(network.Name.ToLower()) ? Chain.Mainchain : Chain.Sidechain;
        }

        public static Network ToCounterChainNetwork(this Network network)
        {
            if (network.Name.ToLower() == MainChainNames[0]) return ApexNetwork.Main;
            if (network.Name.ToLower() == MainChainNames[1]) return ApexNetwork.Test;
            if (network.Name.ToLower() == MainChainNames[2]) return ApexNetwork.RegTest;
            if (network.Name.ToLower() == ApexNetwork.Main.Name.ToLower()) return new StratisMain();
            if (network.Name.ToLower() == ApexNetwork.Test.Name.ToLower()) return new StratisTest();
            if (network.Name.ToLower() == ApexNetwork.RegTest.Name.ToLower()) return new StratisRegTest();
            throw new System.ArgumentException("Unknown network.");
        }
    }
}