using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.Features.Dns.Tests
{
    /// <summary>
    /// Tests for the <see cref="DnsFeature"/> class.
    /// </summary>
    public class GivenADnsFeature
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void WhenConstructorCalledAndPeerAddressManagerIsNullThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            Action a = () => { new DnsFeature(null, null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("peerAddressManager");
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void WhenConstructorCalledAndLoggerFactoryIsNullThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            Action a = () => { new DnsFeature(peerAddressManager, null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("loggerFactory");
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void WhenConstructorCalledAndAllParametersValidThenTypeCreated()
        {
            // Arrange.
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;

            // Act.
            DnsFeature feature = new DnsFeature(peerAddressManager, loggerFactory);

            // Assert.
            feature.Should().NotBeNull();
        }
    }
}
