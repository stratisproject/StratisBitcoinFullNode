using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class AddressTests
    {
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

        private static readonly byte[] address2Bytes = new byte[20]
        {
            0xAB, 0xCD, 0xEF, 0xFE, 0xDC, 0xBA, 0x01, 0x20, 0x03, 0x30,
            0x04, 0x40, 0x05, 0x50, 0x06, 0x60, 0x07, 0x70, 0x08, 0x80
        };

        private static Address address0 = Address.Create(address0Bytes);
        private static Address address1 = Address.Create(address1Bytes);
        private static Address address2 = Address.Create(address2Bytes);

        [Fact]
        public void Create_Address_Success()
        {
            Assert.True(address0.ToBytes().SequenceEqual(address0Bytes));
            Assert.True(address1.ToBytes().SequenceEqual(address1Bytes));
            Assert.True(address2.ToBytes().SequenceEqual(address2Bytes));
        }

        [Fact]
        public void UInt160_RoundTrip_Address_Success()
        {
            var uint160 = address2.ToUint160();
            Assert.Equal(address2, uint160.ToAddress());
        }

        [Fact]
        public void Address_ToString()
        {
            var addressString = address2Bytes.ToHexString();

            Assert.Equal(addressString, address2.ToString());
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