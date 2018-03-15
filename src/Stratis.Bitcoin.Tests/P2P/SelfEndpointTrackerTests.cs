using System;
using System.Net;
using Moq;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public class SelfEndpointTrackerTests
    {
        private readonly Mock<IDateTimeProvider> mockDatetimeProvider;
        private readonly ISelfEndpointTracker selfEndpointTracker;

        public SelfEndpointTrackerTests()
        {
            this.mockDatetimeProvider = new Mock<IDateTimeProvider>();
            this.selfEndpointTracker = new SelfEndpointTracker(this.mockDatetimeProvider.Object);
        }

        [Fact]
        public void Add_OneIpEndpoint_GetsAdded()
        {
            this.selfEndpointTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            Assert.True(this.selfEndpointTracker.IsSelf(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234)));
        }

        [Fact]
        public void Add_TwoIpEndpointsTheSame_GetsAddedOnce()
        {
            this.selfEndpointTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            this.selfEndpointTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            Assert.True(this.selfEndpointTracker.IsSelf(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234)));
        }

        [Fact]
        public void Add_TwoDifferentIpEndpoints_BothAreSelf()
        {
            this.selfEndpointTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            this.selfEndpointTracker.Add(new IPEndPoint(IPAddress.Parse("5.6.7.8"), 1234));
            Assert.True(this.selfEndpointTracker.IsSelf(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234)));
            Assert.True(this.selfEndpointTracker.IsSelf(new IPEndPoint(IPAddress.Parse("5.6.7.8"), 1234)));
        }

        [Fact]
        public void IsSelf_ForDifferentEndpoint_IsFalse()
        {
            this.selfEndpointTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            Assert.False(this.selfEndpointTracker.IsSelf(new IPEndPoint(IPAddress.Parse("5.6.7.8"), 1234)));
        }

        [Fact]
        public void IsSelf_ForSameEndpointButExpired_IsFalse()
        {
            this.selfEndpointTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));

            this.SimulateMovingForwardInTimeByExpiryHours();

            Assert.False(this.selfEndpointTracker.IsSelf(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234)));
        }

        private void SimulateMovingForwardInTimeByExpiryHours()
        {
            this.mockDatetimeProvider.Setup(x => x.GetUtcNow())
                .Returns(DateTime.UtcNow.AddHours(SelfEndpointTracker.ExpiryInHours));
        }
    }
}
