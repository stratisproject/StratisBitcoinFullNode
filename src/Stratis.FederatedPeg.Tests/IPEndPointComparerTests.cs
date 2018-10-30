using System.Net;
using FluentAssertions;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class IPEndPointComparerTests
    {
        [Fact]
        public void IPEndPointComparerShouldCompareV4AndV6IPEndpointsCorrectly()
        {
            var comparer = new IPEndPointComparer();

            var endpoint1 = new IPEndPoint(IPAddress.Parse("127.0.0.1").MapToIPv4(), 12857);
            var endpoint2 = new IPEndPoint(IPAddress.Parse("127.0.0.1").MapToIPv6(), 12857);
            comparer.Equals(endpoint2, endpoint1).Should().BeTrue();

            endpoint1 = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12857);
            endpoint2 = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12857);
            comparer.Equals(endpoint2, endpoint1).Should().BeTrue();

            endpoint1 = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12858);
            endpoint2 = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12857);
            comparer.Equals(endpoint2, endpoint1).Should().BeFalse();

            endpoint1 = new IPEndPoint(IPAddress.Parse("127.1.0.1"), 12858);
            endpoint2 = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12857);
            comparer.Equals(endpoint2, endpoint1).Should().BeFalse();
        }

        [Fact]
        public void IPEndPointComparerShouldCompareParsedV4AndV6IPEndpointsCorrectly()
        {
            var comparer = new IPEndPointComparer();

            var endpoint1 = new IPEndPoint(IPAddress.Parse("::ffff:127.0.0.1"), 12857);
            var endpoint2 = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12857);
            comparer.Equals(endpoint2, endpoint1).Should().BeTrue();

            endpoint1 = new IPEndPoint(IPAddress.Parse("::ffff:127.0.0.1"), 12857);
            endpoint2 = new IPEndPoint(IPAddress.Parse("127.1.0.1"), 12857);
            comparer.Equals(endpoint2, endpoint1).Should().BeFalse();

            endpoint1 = new IPEndPoint(IPAddress.Parse("::ffff:127.0.0.1"), 12857);
            endpoint2 = new IPEndPoint(IPAddress.Parse("127.1.0.1"), 12858);
            comparer.Equals(endpoint2, endpoint1).Should().BeFalse();
        }
    }
}
