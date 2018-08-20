using System;
using System.Net;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public class SelfEndpointTrackerTests
    {
        private readonly SelfEndpointTracker selfEndpointTracker;

        private readonly ExtendedLoggerFactory extendedLoggerFactory;

        public SelfEndpointTrackerTests()
        {
            this.extendedLoggerFactory = new ExtendedLoggerFactory();
            this.selfEndpointTracker = new SelfEndpointTracker(this.extendedLoggerFactory);
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
        public void UpdateExternalAddressOnly_IsFinal_ExternalAddressIsUnchanged_AndPeerScoreIsDefault()
        {
            var oldIpEndpoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234);
            var newIpEndpoint = new IPEndPoint(IPAddress.Parse("5.6.7.8"), 5678);

            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(oldIpEndpoint, true);
            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(newIpEndpoint, false);

            Assert.True(this.selfEndpointTracker.MyExternalAddress.Equals(oldIpEndpoint));
            Assert.True(this.selfEndpointTracker.MyExternalAddressPeerScore == 0);
        }

        [Fact]
        public void UpdateWithSameExternalAddress_IsNotFinal_ExternalAddressIsUnchanged_AndPeerScoreIsIncremented()
        {
            var oldIpEndpoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234);
            const int initialPeerScore = 1;

            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(oldIpEndpoint, false);
            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(oldIpEndpoint, false);

            Assert.True(this.selfEndpointTracker.MyExternalAddress.Equals(oldIpEndpoint));
            Assert.Equal(initialPeerScore + 1, this.selfEndpointTracker.MyExternalAddressPeerScore);
        }

        [Fact]
        public void UpdateWithDifferentExternalAddress_IsNotFinal_ExternalAddressIsUnchanged_AndPeerScoreIsDecremented()
        {
            var oldIpEndpoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 1234);
            var newIpEndpoint = new IPEndPoint(IPAddress.Parse("5.6.7.8"), 5678);
            
            const int initialPeerScore = 2;
            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(oldIpEndpoint, false);
            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(oldIpEndpoint, false);
            Assert.Equal(initialPeerScore, this.selfEndpointTracker.MyExternalAddressPeerScore);

            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(newIpEndpoint, false);
            Assert.True(this.selfEndpointTracker.MyExternalAddress.Equals(oldIpEndpoint));
            Assert.Equal(initialPeerScore - 1, this.selfEndpointTracker.MyExternalAddressPeerScore);
        }

        [Fact]
        public void UpdateWithDifferentExternalAddress_IsNotFinal_ExternalAddressIsChanged_AndPeerScoreIsResetTo_1()
        {
            var oldIpEndpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);
            var newIpEndpoint1 = new IPEndPoint(IPAddress.Parse("0.0.0.1"), 1);
            var newIpEndpoint2 = new IPEndPoint(IPAddress.Parse("0.0.0.2"), 2);
            var newIpEndpoint3 = new IPEndPoint(IPAddress.Parse("0.0.0.3"), 3);
            
            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(oldIpEndpoint, false);

            // When count reaches zero external address updates and score reset to 1.
            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(newIpEndpoint1, false);
            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(newIpEndpoint2, false);
            this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(newIpEndpoint3, false);

            Assert.True(this.selfEndpointTracker.MyExternalAddress.Equals(newIpEndpoint3));
            Assert.Equal(1, this.selfEndpointTracker.MyExternalAddressPeerScore);
        }

    }
}
