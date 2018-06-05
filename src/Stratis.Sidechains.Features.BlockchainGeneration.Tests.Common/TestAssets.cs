using NBitcoin;
using FluentAssertions;

using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common
{
    internal class TestAssets
    {
        internal const string EnigmaChainName = "enigma";
        internal const string MysteryChainName = "mystery";

        internal class SidechainInfoBaseParams
        {
            public uint TimeBase;
            public uint NonceBase;
            public uint MessageStartBase;
            public string CoinSymbolBase;
            public string CoinNameBase;
            public int CoinTypeBase;
            public int PortBase;
            public int RpcPortBase;
            public int ApiPortBase;
            public int AddressPrefixBase;

            public static SidechainInfoBaseParams Default = new SidechainInfoBaseParams()
            {
                TimeBase = 1510170000,
                NonceBase = 2500000,
                MessageStartBase = 7846846,
                CoinSymbolBase = "EGA",
                CoinNameBase = EnigmaChainName+"Coin",
                CoinTypeBase = 12345,
                PortBase = 36000,
                RpcPortBase = 36100,
                ApiPortBase = 36200,
                AddressPrefixBase = 45
            };

            public static NetworkInfo GetNetworkInfo(string networkName, uint seed, uint offset)
            {
                var localSeed = seed + offset;
                string genesisHasHex = networkName == SidechainNetwork.SidechainMainName
                    ? "03ca8b76093da3e9132a6f1002ce9e95468ae538f10ea5eb23c594265e3bcbea"
                    : networkName == SidechainNetwork.SidechainTestName
                    ? "6ea951942c42d3e92fc08a13567ff6974a0eb5f9cbdc331b58356990edab9d6f"
                    : networkName == SidechainNetwork.SidechainRegTestName
                    ? "d82276367430662e95e40f704ddde6b00fb07921c3bd51dd00e17d0d933a12ab"
                    : "0";

                var networkInfo = new NetworkInfo(networkName,
                    Default.TimeBase + localSeed,
                    Default.NonceBase + localSeed,
                    Default.MessageStartBase + localSeed,
                    Default.AddressPrefixBase + (int)localSeed,
                    Default.PortBase + (int)localSeed,
                    Default.RpcPortBase + (int)localSeed,
                    Default.ApiPortBase + (int)localSeed,
                    Default.CoinSymbolBase + localSeed,
                    genesisHasHex);

                return networkInfo;
            }
        }

        internal SidechainInfo GetSidechainInfo(string name, uint seed)
        {
            var mainNet = SidechainInfoBaseParams.GetNetworkInfo(SidechainNetwork.SidechainMainName, seed, 0);
            var testNet = SidechainInfoBaseParams.GetNetworkInfo(SidechainNetwork.SidechainTestName, seed, 1);
            var regTest = SidechainInfoBaseParams.GetNetworkInfo(SidechainNetwork.SidechainRegTestName, seed, 2);
            return new SidechainInfo(name, 
                SidechainInfoBaseParams.Default.CoinNameBase, 
                SidechainInfoBaseParams.Default.CoinTypeBase, 
                mainNet, testNet, regTest);
        }

        internal SidechainInfoRequest GetSidechainInfoRequest(string name, uint seed)
        {
            var mainNet = (NetworkInfoRequest)SidechainInfoBaseParams.GetNetworkInfo(SidechainNetwork.SidechainMainName, seed, 0);
            var testNet = (NetworkInfoRequest)SidechainInfoBaseParams.GetNetworkInfo(SidechainNetwork.SidechainTestName, seed, 1);
            var regTest = (NetworkInfoRequest)SidechainInfoBaseParams.GetNetworkInfo(SidechainNetwork.SidechainRegTestName, seed, 2);
            return new SidechainInfoRequest(name, 
                SidechainInfoBaseParams.Default.CoinNameBase,
                SidechainInfoBaseParams.Default.CoinTypeBase,
                mainNet, testNet, regTest);
        }

        internal void VerifySidechainInfo(SidechainInfo sidechainInfo, string chainName, uint seed)
        {
            sidechainInfo.ChainName.Should().Be(chainName);

            VerifyNetworkInfo(sidechainInfo.MainNet, seed, 0);
            VerifyNetworkInfo(sidechainInfo.TestNet, seed, 1);
            VerifyNetworkInfo(sidechainInfo.RegTest, seed, 2);
        }

        private static void VerifyNetworkInfo(NetworkInfo networkInfo, uint seed, uint offset)
        {
            var localSeed = seed + offset;
            networkInfo.Time.Should().Be(SidechainInfoBaseParams.Default.TimeBase + localSeed);
            networkInfo.Nonce.Should().Be(SidechainInfoBaseParams.Default.NonceBase + localSeed);
            networkInfo.MessageStart.Should().Be(SidechainInfoBaseParams.Default.MessageStartBase + localSeed);
            networkInfo.AddressPrefix.Should().Be((int)(SidechainInfoBaseParams.Default.AddressPrefixBase + localSeed));

            networkInfo.Port.Should().Be((int)(SidechainInfoBaseParams.Default.PortBase + localSeed));
            networkInfo.RpcPort.Should().Be((int)(SidechainInfoBaseParams.Default.RpcPortBase + localSeed));
            networkInfo.ApiPort.Should().Be((int)(SidechainInfoBaseParams.Default.ApiPortBase + localSeed));

            networkInfo.CoinSymbol.Should().Be(SidechainInfoBaseParams.Default.CoinSymbolBase + localSeed);
            //TODO: check if it is OK to have GenesisHash left undefined
            //networkInfo.GenesisHashHex.Should().NotBeNullOrWhiteSpace();
            if (!string.IsNullOrWhiteSpace(networkInfo.GenesisHashHex))
            {
                networkInfo.GenesisHash.Should().Be(uint256.Parse(networkInfo.GenesisHashHex));
                if (seed == 0)
                {
                    if (networkInfo.NetworkName == SidechainNetwork.SidechainMainName)
                        networkInfo.GenesisHashHex.Should().Be("03ca8b76093da3e9132a6f1002ce9e95468ae538f10ea5eb23c594265e3bcbea");
                    else if (networkInfo.NetworkName == SidechainNetwork.SidechainTestName)
                        networkInfo.GenesisHashHex.Should().Be("6ea951942c42d3e92fc08a13567ff6974a0eb5f9cbdc331b58356990edab9d6f");
                    else if (networkInfo.NetworkName == SidechainNetwork.SidechainRegTestName)
                        networkInfo.GenesisHashHex.Should().Be("d82276367430662e95e40f704ddde6b00fb07921c3bd51dd00e17d0d933a12ab");
                }
            }
        }
    }
}