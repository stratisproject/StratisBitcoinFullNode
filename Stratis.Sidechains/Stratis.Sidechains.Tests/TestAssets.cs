using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NBitcoin;

namespace Stratis.Sidechains.Tests
{
    internal class TestAssets
    {
        uint timeBase = 1510170000;
        uint nonceBase = 2500000;
        int portBase = 36000;
        int rpcPortBase = 36100;
        int addressPrefixBase = 45;

        internal SidechainInfo GetSidechainInfo(string name, uint seed)
        {
            var mainNet = new NetworkInfo("SidechainMain", timeBase + seed, nonceBase + seed,
                portBase + (int) seed,
                rpcPortBase + (int) seed, addressPrefixBase + (int) seed);
            var testNet = new NetworkInfo( "SidechainTestNet", timeBase + seed + 1, nonceBase + seed + 1,
                portBase + (int) seed + 1,
                rpcPortBase + (int) seed + 1, addressPrefixBase + (int) seed + 1);
            var regTest = new NetworkInfo( "SidechainRegTest", timeBase + seed + 2, nonceBase + seed + 2,
                portBase + (int) seed + 2,
                rpcPortBase + (int) seed + 2, addressPrefixBase + (int) seed + 2);
            return new SidechainInfo(name, mainNet, testNet, regTest);
        }

        internal void VerifySidechainInfo(SidechainInfo sidechainInfo, string name, uint seed)
        {
            sidechainInfo.Name.Should().Be(name);

            //mainnet
            sidechainInfo.MainNet.Time.Should().Be(timeBase + seed);
            sidechainInfo.MainNet.Nonce.Should().Be(nonceBase + seed);
            sidechainInfo.MainNet.Port.Should().Be((int) (portBase + seed));
            sidechainInfo.MainNet.RpcPort.Should().Be((int) (rpcPortBase + seed));
            sidechainInfo.MainNet.AddressPrefix.Should().Be((int) (addressPrefixBase + seed));

            //mainnet
            sidechainInfo.TestNet.Time.Should().Be(timeBase + seed + 1);
            sidechainInfo.TestNet.Nonce.Should().Be(nonceBase + seed + 1);
            sidechainInfo.TestNet.Port.Should().Be((int) (portBase + seed + 1));
            sidechainInfo.TestNet.RpcPort.Should().Be((int) (rpcPortBase + seed + 1));
            sidechainInfo.TestNet.AddressPrefix.Should().Be((int) (addressPrefixBase + seed + 1));

            //regtest
            sidechainInfo.RegTest.Time.Should().Be(timeBase + seed + 2);
            sidechainInfo.RegTest.Nonce.Should().Be(nonceBase + seed + 2);
            sidechainInfo.RegTest.Port.Should().Be((int) (portBase + seed + 2));
            sidechainInfo.RegTest.RpcPort.Should().Be((int) (rpcPortBase + seed + 2));
            sidechainInfo.RegTest.AddressPrefix.Should().Be((int) (addressPrefixBase + seed + 2));

            if (seed == 0)
            {
                //these are known hashes for the 0 seed
                sidechainInfo.MainNet.GenesisHash.Should().Be(uint256.Parse("b21e5b2a32303f254cb0cd679fd179898dfbac2c3c06890d07576766b4074311"));
                sidechainInfo.MainNet.GenesisHashHex.Should().Be("b21e5b2a32303f254cb0cd679fd179898dfbac2c3c06890d07576766b4074311");

                sidechainInfo.TestNet.GenesisHash.Should().Be(uint256.Parse("6ea951942c42d3e92fc08a13567ff6974a0eb5f9cbdc331b58356990edab9d6f"));
                sidechainInfo.TestNet.GenesisHashHex.Should().Be("6ea951942c42d3e92fc08a13567ff6974a0eb5f9cbdc331b58356990edab9d6f");

                sidechainInfo.RegTest.GenesisHash.Should().Be(uint256.Parse("d82276367430662e95e40f704ddde6b00fb07921c3bd51dd00e17d0d933a12ab"));
                sidechainInfo.RegTest.GenesisHashHex.Should().Be("d82276367430662e95e40f704ddde6b00fb07921c3bd51dd00e17d0d933a12ab");
            }
        }
    }
}