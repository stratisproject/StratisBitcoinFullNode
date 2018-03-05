using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Moq;
using NBitcoin;
using Xunit;

namespace Stratis.Sidechains.Tests
{
    public class Network_Shall
    {
        private TestAssets testAssets = new TestAssets();

        [Fact]
        public void use_provider_to_create_genesis_hash()
        {
            var sidechainInfoProvider = new Mock<ISidechainInfoProvider>();
            sidechainInfoProvider.Setup(m => m.GetSidechainInfo("enigma"))
                .Returns(this.testAssets.GetSidechainInfo("enigma", 0));

            using (var sidechainIdentifier = SidechainIdentifier.Create("enigma", sidechainInfoProvider.Object))
            {
                var network = Network.SidechainTestNet;
                network.Name.Should().Be("SidechainTestNet");
                network.DefaultPort.Should().Be(36001);
                network.RPCPort.Should().Be(36101);
                network.GenesisHash.Should().Be(uint256.Parse("6ea951942c42d3e92fc08a13567ff6974a0eb5f9cbdc331b58356990edab9d6f"));
                network.base58Prefixes[0][0].Should().Be(46);
            }
        }
    }
}
