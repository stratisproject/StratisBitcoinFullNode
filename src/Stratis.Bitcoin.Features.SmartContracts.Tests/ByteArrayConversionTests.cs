using System;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ByteArrayConversionTests
    {
        // The smart contract being tested contains a bunch of static methods related to the conversion of bytes to smart contract primitives.
        // It's also validated inside DeterminismValidationTests so we know it is deterministic too.
        // Serves as a good example for future documentation.

        [Fact]
        public void UIntConversion()
        {
            // TODO: Test more values.
            byte[] max = new byte[] { 255, 255, 255, 255 };
            uint expected = BitConverter.ToUInt32(max);
            uint actual = ByteArrayConversion.BytesToUInt(max);
            Assert.Equal(expected, actual);

            byte[] expectedBytes = BitConverter.GetBytes(uint.MaxValue);
            byte[] actualBytes = ByteArrayConversion.UIntToBytes(uint.MaxValue);
            Assert.Equal(expectedBytes, actualBytes);
        }

        [Fact]
        public void IntConversion()
        {
            // TODO: Test more values.
            byte[] max = new byte[] { 255, 255, 255, 255 };
            int expected = BitConverter.ToInt32(max);
            int actual = ByteArrayConversion.BytesToInt(max);
            Assert.Equal(expected, actual);

            byte[] expectedBytes = BitConverter.GetBytes(int.MaxValue);
            byte[] actualBytes = ByteArrayConversion.IntToBytes(int.MaxValue);
            Assert.Equal(expectedBytes, actualBytes);
        }


    }
}
