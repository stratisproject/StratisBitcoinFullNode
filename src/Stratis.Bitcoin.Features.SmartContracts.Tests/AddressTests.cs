using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class AddressTests
    {
        private static readonly Network network = new SmartContractPosRegTest();

        private static readonly byte[] address0Bytes = new byte[20]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] address1Bytes = new byte[20]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
        };

        private static Address address0 = Address.Create(address0Bytes, address0Bytes.BytesToAddressString(network));
        private static Address address1 = Address.Create(address1Bytes, address1Bytes.BytesToAddressString(network));

        [Fact]
        public void Address_ToString()
        {
            var addressString = address0Bytes.BytesToAddressString(network);

            Assert.Equal(addressString, address0.ToString());
        }

        [Fact]
        public void Address_Equality_Equals_Different()
        {
            Assert.False(address0.Equals(address1));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Different()
        {
            Assert.False(address0 == address1);
        }

        [Fact]
        public void Address_Equality_Equals_Same()
        {
            var address2 = new Address(address1);

            Assert.True(address1.Equals(address2));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Same()
        {
            var address2 = new Address(address1);

            Assert.True(address1 == address2);
        }

        [Fact]
        public void Address_Equality_Equals_Same_Instance()
        {
            Assert.True(address0.Equals(address0));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Same_Instance()
        {
            Assert.True(address0 == address0);
        }

        [Fact]
        public void Address_Equality_Equals_Null()
        {
            Assert.False(address0.Equals(null));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Null()
        {
            Assert.False(address0 == null);
        }
    }
}