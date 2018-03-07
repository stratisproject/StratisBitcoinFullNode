using System;
using System.Net;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    /// <summary>
    /// Directly tests <see cref="IPAddressGenerator"/> which is used by tests
    /// </summary>
    public class IPAddressGeneratorTest
    {
        [Fact]
        public void GetNext_ReturnsNextIPAddress()
        {
            IPAddressGenerator ipAddressGenerator = new IPAddressGenerator(IPAddress.Parse("2.2.2.2"));
            IPAddress next = ipAddressGenerator.GetNext();
            Assert.Equal(IPAddress.Parse("2.2.2.3"), next);

            ipAddressGenerator = new IPAddressGenerator(IPAddress.Parse("2.2.2.254"));
            next = ipAddressGenerator.GetNext();
            Assert.Equal(IPAddress.Parse("2.2.3.1"), next);

            ipAddressGenerator = new IPAddressGenerator(IPAddress.Parse("2.2.254.254"));
            next = ipAddressGenerator.GetNext();
            Assert.Equal(IPAddress.Parse("2.3.1.1"), next);

            ipAddressGenerator = new IPAddressGenerator(IPAddress.Parse("2.254.254.254"));
            next = ipAddressGenerator.GetNext();
            Assert.Equal(IPAddress.Parse("3.1.1.1"), next);
        }

        [Fact]
        public void GetNext_WhenNextAddressOutOfRange_Exceptions()
        {
            try
            {
                new IPAddressGenerator(IPAddress.Parse("254.254.254.254")).GetNext();
                Assert.False(true, "Should have exceptioned.");
            }
            catch (Exception exception)
            {
                Assert.IsType<IPAddressGenerator.IPAddressOutOfRangeException>(exception);
            }
        }
    }
}