using FluentAssertions;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.FederatedPeg.Features.FederationGateway.Federation;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class FederationMemberPrivate_Shall
    {
        [Fact]
        public void create_a_federation_member()
        {
            //use know values to test
            var publicKeyMainchain =
                new Key(Encoders.Hex.DecodeData("ba45a6e9f3f4b203699a37b4f5a91d74bc24d1a49b4e63374c2d7e6efcc54914"));
            var publicKeySidechain =
                new Key(Encoders.Hex.DecodeData("7858de78e18347540a435142d5c86eded978c965b39cee465b2ef9e21d6c7e7a"));

            var federationMemberPrivate =
                new FederationMemberPrivate("Bob", "passPhrase", publicKeyMainchain, publicKeySidechain);
            federationMemberPrivate.Name.Should().Be("Bob");
        }

        [Fact]
        public void creates_a_public_federation_member()
        {
            var federationMemberPrivate = FederationMemberPrivate.CreateNew("Alice", "password");
            federationMemberPrivate.ToFederationMember().GetType().Should().Be(typeof(FederationMember));
            federationMemberPrivate.ToFederationMember().Name.Should().Be("Alice");
        }
    }
}
