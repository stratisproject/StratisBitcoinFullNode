using System;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class CollateralPoAConsensusFactoryTests
    {
        private readonly CollateralPoAConsensusFactory factory;

        public CollateralPoAConsensusFactoryTests()
        {
            this.factory = new CollateralPoAConsensusFactory();
        }

        [Fact]
        public void CanSerializeAndDeserializeFederationMember()
        {
            var federationMember = new CollateralFederationMember(new Key().PubKey, new Money(999), "addr1");

            byte[] serializedBytes = this.factory.SerializeFederationMember(federationMember);

            var deserializedMember = this.factory.DeserializeFederationMember(serializedBytes) as CollateralFederationMember;

            Assert.Equal(federationMember, deserializedMember);
        }

        [Fact]
        public void ThrowsIfIncorrectType()
        {
            var federationMember = new FederationMember(new Key().PubKey);

            Assert.Throws<ArgumentException>(() => this.factory.SerializeFederationMember(federationMember));
        }
    }
}
