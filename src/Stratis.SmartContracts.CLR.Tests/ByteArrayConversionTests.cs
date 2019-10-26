using System;
using Stratis.SmartContracts.Core;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
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

        [Fact]
        public void ULongConversion()
        {
            // TODO: Test more values.
            byte[] max = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 };
            ulong expected = BitConverter.ToUInt64(max);
            ulong actual = ByteArrayConversion.BytesToULong(max);
            Assert.Equal(expected, actual);

            byte[] expectedBytes = BitConverter.GetBytes(ulong.MaxValue);
            byte[] actualBytes = ByteArrayConversion.ULongToBytes(ulong.MaxValue);
            Assert.Equal(expectedBytes, actualBytes);
        }

        [Fact]
        public void LongConversion()
        {
            // TODO: Test more values.
            byte[] max = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 };
            long expected = BitConverter.ToInt64(max);
            long actual = ByteArrayConversion.BytesToLong(max);
            Assert.Equal(expected, actual);

            byte[] expectedBytes = BitConverter.GetBytes(long.MaxValue);
            byte[] actualBytes = ByteArrayConversion.LongToBytes(long.MaxValue);
            Assert.Equal(expectedBytes, actualBytes);
        }

        [Fact]
        public void HexConversion()
        {
            string hex = "FC65A311";
            byte[] expected = hex.HexToByteArray();
            byte[] actual = ByteArrayConversion.HexStringToBytes(hex);
            Assert.Equal(expected, actual);

            byte[] bytes = new byte[] { 1, 2, 3, 4 };
            string expectedString = BitConverter.ToString(bytes).Replace("-", "");
            string actualString = ByteArrayConversion.BytesToHexString(bytes);
            Assert.Equal(expectedString, actualString);
        }
        
    }
}
