using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    /// <summary>
    /// A container for storing/retrieving known networks
    /// </summary>
    public static class NetworksContainer
    {
        public static readonly ConcurrentDictionary<string, Network> Networks = new ConcurrentDictionary<string, Network>();

        /// <summary>
        /// Register an immutable <see cref="Network"/> instance so it is queryable through <see cref="GetNetwork"/> and <see cref="GetNetworks"/>.
        /// </summary>
        public static Network Register(Network network)
        {
            IEnumerable<string> networkNames = network.AdditionalNames != null ? new[] { network.Name }.Concat(network.AdditionalNames) : new[] { network.Name };

            foreach (string networkName in networkNames)
            {
                // Performs a series of checks before registering the network to the list of available networks.
                if (string.IsNullOrEmpty(networkName))
                    throw new InvalidOperationException("A network name needs to be provided.");

                if (GetNetwork(networkName) != null)
                    throw new InvalidOperationException("The network " + networkName + " is already registered.");

                if (network.GetGenesis() == null)
                    throw new InvalidOperationException("A genesis block needs to be provided.");

                if (network.Consensus == null)
                    throw new InvalidOperationException("A consensus needs to be provided.");

                Networks.TryAdd(networkName.ToLowerInvariant(), network);
            }

            return network;
        }

        /// <summary>
        /// Get network from name
        /// </summary>
        /// <param name="name">main,mainnet,testnet,test,testnet3,reg,regtest,seg,segnet</param>
        /// <returns>The network or null of the name does not match any network</returns>
        public static Network GetNetwork(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (Networks.Any())
            {
                name = name.ToLowerInvariant();
                return Networks.TryGet(name);
            }

            return null;
        }

        public static IEnumerable<Network> GetNetworks()
        {
            yield return Network.Main;
            yield return Network.TestNet;
            yield return Network.RegTest;

            if (Networks.Any())
            {
                List<Network> others = Networks.Values.Distinct().ToList();

                foreach (Network network in others)
                    yield return network;
            }
        }

        internal static Network GetNetworkFromBase58Data(string base58, Base58Type? expectedType = null)
        {
            foreach (Network network in GetNetworks())
            {
                Base58Type? type = network.GetBase58Type(base58);
                if (type.HasValue)
                {
                    if (expectedType != null && expectedType.Value != type.Value)
                        continue;
                    if (type.Value == Base58Type.COLORED_ADDRESS)
                    {
                        byte[] raw = Encoders.Base58Check.DecodeData(base58);
                        byte[] version = network.GetVersionBytes(type.Value, false);
                        if (version == null)
                            continue;
                        raw = raw.Skip(version.Length).ToArray();
                        base58 = Encoders.Base58Check.EncodeData(raw);
                        return GetNetworkFromBase58Data(base58, null);
                    }
                    return network;
                }
            }
            return null;
        }
    }
}