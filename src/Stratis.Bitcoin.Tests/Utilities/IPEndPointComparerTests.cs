using System.Net;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class IPEndPointComparerTests
    {
        [Fact]
        public void CanCompareIPEndPoints()
        {
            var ep1 = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 5);
            var ep2 = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 6);
            var ep3 = new IPEndPoint(IPAddress.Parse("1.1.2.2"), 5);
            var ep4 = new IPEndPoint(IPAddress.Parse("1.1.2.2"), 6);
            var ep5 = new IPEndPoint(IPAddress.Parse("1.1.2.2"), 6);

            var comparer = new IPEndPointComparer();

            Assert.False(comparer.Equals(ep1, ep2));
            Assert.False(comparer.Equals(ep1, ep3));
            Assert.True(comparer.Equals(ep4, ep5));

            Assert.NotEqual(comparer.GetHashCode(ep1), comparer.GetHashCode(ep2));
            Assert.NotEqual(comparer.GetHashCode(ep1), comparer.GetHashCode(ep3));
            Assert.Equal(comparer.GetHashCode(ep4), comparer.GetHashCode(ep5));
        }
    }
}
