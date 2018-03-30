using System.Net;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public class SelfEndpointTrackerTests
    {
        private readonly ISelfEndpointTracker selfEndpointTracker;

        public SelfEndpointTrackerTests()
        {
            this.selfEndpointTracker = new SelfEndpointTracker();
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
    }
}
