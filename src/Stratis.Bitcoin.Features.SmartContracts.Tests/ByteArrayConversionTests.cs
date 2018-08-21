﻿using System;
using System.Text;
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

        
    }
}
