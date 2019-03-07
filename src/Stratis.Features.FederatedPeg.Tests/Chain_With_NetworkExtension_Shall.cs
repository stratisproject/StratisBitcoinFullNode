using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Stratis.Features.FederatedPeg.NetworkHelpers;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    [Collection("FederatedPegTests")]
    public class Chain_With_NetworkExtension_Shall
    {
        [Fact(Skip = TestingValues.SkipTests)]
        public void correctly_identify_mainchain()
        {
            var stratisRegTest = new StratisRegTest();
            Chain chain = stratisRegTest.ToChain();
            chain.Should().Be(Chain.Mainchain);
            chain.Should().NotBe(Chain.Sidechain);

            var stratisTest = new StratisTest();
            chain = stratisTest.ToChain();
            chain.Should().Be(Chain.Mainchain);
            chain.Should().NotBe(Chain.Sidechain);

            var stratisMain = new StratisMain();
            chain = stratisMain.ToChain();
            chain.Should().Be(Chain.Mainchain);
            chain.Should().NotBe(Chain.Sidechain);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void correctly_identify_sidechain()
        {	
            Network apexRegTest = FederatedPegNetwork.NetworksSelector.Regtest();
            Chain chain = apexRegTest.ToChain();
            chain.Should().Be(Chain.Sidechain);
            chain.Should().NotBe(Chain.Mainchain);

            Network apexTest = FederatedPegNetwork.NetworksSelector.Testnet();
            chain = apexTest.ToChain();
            chain.Should().Be(Chain.Sidechain);
            chain.Should().NotBe(Chain.Mainchain);

            Network apexMain = FederatedPegNetwork.NetworksSelector.Mainnet();
            chain = apexMain.ToChain();
            chain.Should().Be(Chain.Sidechain);
            chain.Should().NotBe(Chain.Mainchain);
        }
    }
}
