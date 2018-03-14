using System;
using System.Net;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public class SelfEndpointTrackerTests : IClassFixture<SelfEndpointTracker>
    {
        [Fact]
        public void Add_OneIpEndpoint_GetsAdded()
        {
            ISelfEndpointTracker selfEndpointTrackerTracker = new SelfEndpointTracker();
            selfEndpointTrackerTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            Assert.True(selfEndpointTrackerTracker.IsSelf(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234)));
        }

        [Fact]
        public void Add_TwoIpEndpointsTheSame_GetsAddedOnce()
        {
            ISelfEndpointTracker selfEndpointTrackerTracker = new SelfEndpointTracker();
            selfEndpointTrackerTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            selfEndpointTrackerTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            Assert.True(selfEndpointTrackerTracker.IsSelf(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234)));
        }

        [Fact]
        public void Add_TwoDifferentIpEndpoints_BothAreSelf()
        {
            ISelfEndpointTracker selfEndpointTrackerTracker = new SelfEndpointTracker();
            selfEndpointTrackerTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            selfEndpointTrackerTracker.Add(new IPEndPoint(IPAddress.Parse("5.6.7.8"), 1234));
            Assert.True(selfEndpointTrackerTracker.IsSelf(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234)));
            Assert.True(selfEndpointTrackerTracker.IsSelf(new IPEndPoint(IPAddress.Parse("5.6.7.8"), 1234)));
        }

        [Fact]
        public void IsSelf_ForDifferentEndpoint_IsFalse()
        {
            ISelfEndpointTracker selfEndpointTrackerTracker = new SelfEndpointTracker();
            selfEndpointTrackerTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            Assert.False(selfEndpointTrackerTracker.IsSelf(new IPEndPoint(IPAddress.Parse("5.6.7.8"), 1234)));
        }

        [Fact]
        public void IsSelf_ForSameEndpointButExpired_IsFalse()
        {
            SelfEndpointTracker selfEndpointTrackerTracker = new SelfEndpointTracker();
            selfEndpointTrackerTracker.Add(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234));
            this.SimulateMovingForwardInTimeByExpiryHours(selfEndpointTrackerTracker);
            Assert.False(selfEndpointTrackerTracker.IsSelf(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234)));
        }

        private void SimulateMovingForwardInTimeByExpiryHours(SelfEndpointTracker selfEndpointTrackerTracker)
        {
            selfEndpointTrackerTracker.Now = () => DateTime.UtcNow.AddHours(SelfEndpointTracker.ExpiryInHours);
        }
    }
}
