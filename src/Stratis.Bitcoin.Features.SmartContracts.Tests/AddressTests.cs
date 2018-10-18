using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class AddressTests
    {
        private static readonly Network network = new SmartContractPosRegTest();

        private static readonly byte[] address0 = new byte[20]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] address1 = new byte[20]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
        };

        [Fact]
        public void Address_ToString()
        {
            var address = Address.Create(address0, new SmartContractPosRegTest());

            var str = address.ToString();
        }

        [Fact]
        public void Address_Equality_Equals_Different()
        {
            var address = Address.Create(address0, network);

            var address2 = Address.Create(address1, network);

            Assert.False(address.Equals(address2));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Different()
        {
            var address = Address.Create(address0, network);
            var address2 = Address.Create(address1, network);

            Assert.False(address == address2);
        }

        [Fact]
        public void Address_Equality_Equals_Same()
        {
            var address = Address.Create(address0, network);

            var address2 = new Address(address);

            Assert.True(address.Equals(address2));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Same()
        {
            var address = Address.Create(address0, network);
            var address2 = new Address(address);

            Assert.True(address == address2);
        }

        [Fact]
        public void Address_Equality_Equals_Same_Instance()
        {
            var address = Address.Create(address0, network);

            Assert.True(address.Equals(address));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Same_Instance()
        {
            var address = Address.Create(address0, network);

            Assert.True(address == address);
        }

        [Fact]
        public void Address_Equality_Equals_Null()
        {
            var address = Address.Create(address0, network); ;

            Assert.False(address.Equals(null));
        }

        [Fact]
        public void Address_Equality_Equals_Operator_Null()
        {
            var address = Address.Create(address0, network);

            Assert.False(address == null);
        }
    }
}