using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NBitcoin;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class FederationMember_Shall
    {
        [Fact]
        public void create_a_federation_member()
        {
            var publicKeyMainchain = new Key().PubKey;
            var publicKeySidechain = new Key().PubKey;

            var federationMember = new FederationMember("Bob", publicKeyMainchain, publicKeySidechain);
            federationMember.Name.Should().Be("Bob");
            federationMember.PublicKeyMainChain.Should().Be(publicKeyMainchain);
            federationMember.PublicKeySideChain.Should().Be(publicKeySidechain);
        }
    }
}
