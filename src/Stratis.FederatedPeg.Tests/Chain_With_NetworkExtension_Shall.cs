using FluentAssertions;
using NBitcoin;
using Stratis.Sidechains.Features.BlockchainGeneration;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    [Collection("FederatedPegTests")]
    public class Chain_With_NetworkExtension_Shall
    {
        [Fact]
        public void correctly_identify_mainchain()
        {
            //reg test
            var network = Network.StratisRegTest;
            var chain = network.ToChain();
            chain.Should().Be(Chain.Mainchain);
            chain.Should().NotBe(Chain.Sidechain);

            //testnet
            network = Network.StratisTest;
            chain = network.ToChain();
            chain.Should().Be(Chain.Mainchain);
            chain.Should().NotBe(Chain.Sidechain);

            //mainnet
            network = Network.StratisMain;
            chain = network.ToChain();
            chain.Should().Be(Chain.Mainchain);
            chain.Should().NotBe(Chain.Sidechain);
        }

        [Fact]
        public void correctly_identify_sidechain()
        {
            //reg test	
            var network = SidechainNetwork.SidechainRegTest;
            var chain = network.ToChain();
            chain.Should().Be(Chain.Sidechain);
            chain.Should().NotBe(Chain.Mainchain);

            //testnet	
            network = SidechainNetwork.SidechainTest;
            chain = network.ToChain();
            chain.Should().Be(Chain.Sidechain);
            chain.Should().NotBe(Chain.Mainchain);

            //mainnet	
            network = SidechainNetwork.SidechainMain;
            chain = network.ToChain();
            chain.Should().Be(Chain.Sidechain);
            chain.Should().NotBe(Chain.Mainchain);
        }
    }
}
