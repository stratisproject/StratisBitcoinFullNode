using Stratis.SmartContracts;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class AddressTests
    {
        [Fact]
        public void Address_Equality_Equals_Different()
        {
            var address = new Address("address1");

            var address2 = new Address("address2");

            Assert.False(address.Equals(address2));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Different()
        {
            var address = new Address("address1");
            var address2 = new Address("address2");

            Assert.False(address == address2);
        }

        [Fact]
        public void Address_Equality_Equals_Same()
        {
            var address = new Address("address1");

            var address2 = new Address(address.Value);

            Assert.True(address.Equals(address2));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Same()
        {
            var address = new Address("address1");
            var address2 = new Address(address.Value);

            Assert.True(address == address2);
        }

        [Fact]
        public void Address_Equality_Equals_Same_Instance()
        {
            var address = new Address("address1");

            Assert.True(address.Equals(address));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Same_Instance()
        {
            var address = new Address("address1");

            Assert.True(address == address);
        }

        [Fact]
        public void Address_Equality_Equals_Null()
        {
            var address = new Address("address1");

            Assert.False(address.Equals(null));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Null()
        {
            var address = new Address("address1");

            Assert.False(address == null);
        }
    }
}