using NBitcoin;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class AddressTests
    {
        private static readonly byte[] AddressBytes = new byte[20]
        {
            0xAB, 0xCD, 0xEF, 0xFE, 0xDC, 0xBA, 0x01, 0x20, 0x03, 0x30,
            0x04, 0x40, 0x05, 0x50, 0x06, 0x60, 0x07, 0x70, 0x08, 0x80
        };

        private static readonly Address Address = AddressBytes.ToAddress();

        [Fact]
        public void UInt160_RoundTrip_Address_Success()
        {
            uint160 uint160 = Address.ToUint160();
            Assert.Equal(Address, uint160.ToAddress());
        }
    }
}
