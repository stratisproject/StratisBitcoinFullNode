using FluentAssertions;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Stratis.Sidechains.Networks;
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
            var network = ApexNetwork.RegTest;
            var chain = network.ToChain();
            chain.Should().Be(Chain.Sidechain);
            chain.Should().NotBe(Chain.Mainchain);

            //testnet	
            network = ApexNetwork.Test;
            chain = network.ToChain();
            chain.Should().Be(Chain.Sidechain);
            chain.Should().NotBe(Chain.Mainchain);

            //mainnet	
            network = ApexNetwork.Main;
            chain = network.ToChain();
            chain.Should().Be(Chain.Sidechain);
            chain.Should().NotBe(Chain.Mainchain);
        }
    }
}
