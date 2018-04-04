using FluentAssertions;
using NBitcoin;
using NBitcoin.DataEncoders;
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
            federationMemberPrivate.GetEncryptedKey(Chain.Mainchain).Should().Be(
                "EMh7xaKO6nFa0XY9fzmJLn4js3xMPgwyl06Np8pDgfMXfViFVX+POLA1Rv9ChsQugtgCjjLUrLlIcfKh8w/5fdnr4qc1/mmHULGuJFduiV8=");
            federationMemberPrivate.GetEncryptedKey(Chain.Sidechain).Should().Be(
                "Z3/S0l3pRcHMAAVdUZzptOzrwWqkAPjDJWr/ZJCcWKhyN2sx+Gbz3kzmW7bnXKPuuFr0OzHr12JgW941U8MibMtckHn5B2yu5pJzGlAHZmA=");
        }

        [Fact]
        public void creates_a_public_federation_member()
        {
            var federationMemberPrivate = FederationMemberPrivate.CreateNew("Alice", "password");
            federationMemberPrivate.ToFederationMember().GetType().Should().Be(typeof(FederationMember));
            federationMemberPrivate.ToFederationMember().Name.Should().Be("Alice");
        }

        [Fact]
        public void fail_validation_for_invalid_name()
        {
            FederationMemberPrivate.IsValidName(null).Should().BeFalse();
            FederationMemberPrivate.IsValidName(string.Empty).Should().BeFalse();
            FederationMemberPrivate.IsValidName("   ").Should().BeFalse();
            FederationMemberPrivate.IsValidName("B").Should().BeFalse();
            FederationMemberPrivate.IsValidName("Bo").Should().BeFalse();
            FederationMemberPrivate.IsValidName("Bo_Bo").Should().BeFalse();

            FederationMemberPrivate.IsValidName("Bob").Should().BeTrue();
        }
    }
}
