using System;
using Stratis.SmartContracts.ByteHelper;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ByteConverterTests
    {
        // TODO: Move these to new repo with .ByteHelper project when ready.

        [Fact]
        public void ByteToBool()
        {
            Assert.True(ByteConverter.ToBool(123));
            Assert.True(ByteConverter.ToBool(1));
            Assert.False(ByteConverter.ToBool(0));
        }

        [Fact]
        public void BoolToByte()
        {
            Assert.Equal(1, ByteConverter.ToByte(true));
            Assert.Equal(0, ByteConverter.ToByte(false));
        }

        [Fact]
        public void Int32ToBytes()
        {
            var random = new Random();
            for(int i=0; i< 10; i++)
            {
                int next = random.Next();
                byte[] expected = BitConverter.GetBytes(next);
                Assert.Equal(expected, ByteConverter.ToBytes(next));
            }
        }

        [Fact]
        public void UInt32ToBytes()
        {
            var random = new Random();
            for (int i = 0; i < 10; i++)
            {
                uint next = (uint) random.Next();
                byte[] expected = BitConverter.GetBytes(next);
                Assert.Equal(expected, ByteConverter.ToBytes(next));
            }
        }

        [Fact]
        public void Int64ToBytes()
        {
            var random = new Random();
            for (int i = 0; i < 10; i++)
            {
                long next = (long) random.Next();
                byte[] expected = BitConverter.GetBytes(next);
                Assert.Equal(expected, ByteConverter.ToBytes(next));
            }
        }

        [Fact]
        public void UInt64ToBytes()
        {
            var random = new Random();
            for (int i = 0; i < 10; i++)
            {
                ulong next = (ulong) random.Next();
                byte[] expected = BitConverter.GetBytes(next);
                Assert.Equal(expected, ByteConverter.ToBytes(next));
            }
        }

        [Fact]
        public void BytesToInt32()
        {
            // Throws exception if too short
            byte[] notLongEnough = new byte[] { 0, 1, 2 };
            Assert.Throws<ArgumentException>(() => ByteConverter.ToInt32(notLongEnough));

            // Works when 4 bytes
            byte[] zero = new byte[] { 0, 0, 0, 0 };
            Assert.Equal(0, ByteConverter.ToInt32(zero));

            // Takes first 4 bytes when longer
            byte[] zeroWithExtra = new byte[] { 0, 0, 0, 0, 1 };
            Assert.Equal(0, ByteConverter.ToInt32(zeroWithExtra));
        }

        [Fact]
        public void BytesToUInt32()
        {
            // Throws exception if too short
            byte[] notLongEnough = new byte[] { 0, 1, 2 };
            Assert.Throws<ArgumentException>(() => ByteConverter.ToUInt32(notLongEnough));

            // Works for 4 bytes
            byte[] zero = new byte[] { 0, 0, 0, 0 };
            Assert.Equal( (uint) 0, ByteConverter.ToUInt32(zero));

            // Takes first 4 bytes when longer
            byte[] zeroWithExtra = new byte[] { 0, 0, 0, 0, 1 };
            Assert.Equal( (uint) 0, ByteConverter.ToUInt32(zeroWithExtra));
        }

        [Fact]
        public void BytesToInt64()
        {
            // Throws exception if too short
            byte[] notLongEnough = new byte[] { 0, 1, 2, 3, 4, 5, 6 };
            Assert.Throws<ArgumentException>(() => ByteConverter.ToInt64(notLongEnough));

            // Works for 8 bytes
            byte[] zero = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            Assert.Equal((long)0, ByteConverter.ToInt64(zero));

            // Takes first 8 bytes when longer
            byte[] zeroWithExtra = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1 };
            Assert.Equal((long)0, ByteConverter.ToInt64(zeroWithExtra));
        }

        [Fact]
        public void BytesToUInt64()
        {
            // Throws exception if too short
            byte[] notLongEnough = new byte[] { 0, 1, 2 };
            Assert.Throws<ArgumentException>(() => ByteConverter.ToUInt64(notLongEnough));

            // Works for 8 bytes
            byte[] zero = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            Assert.Equal((ulong)0, ByteConverter.ToUInt64(zero));

            // Takes first 8 bytes when longer
            byte[] zeroWithExtra = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1 };
            Assert.Equal((ulong)0, ByteConverter.ToUInt64(zeroWithExtra));
        }
    }
}
