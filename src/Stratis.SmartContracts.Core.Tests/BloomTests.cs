using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class BloomTests
    {
        [Fact]
        public void Test_Equality()
        {
            // Inited, both equal
            var bloom1 = new Bloom();
            var bloom2 = new Bloom();
            Assert.Equal(bloom1, bloom2);

            // Which is the same as initing with all 0s
            byte[] zeroedData = new byte[256];
            bloom2 = new Bloom(zeroedData);
            Assert.Equal(bloom1, bloom2);

            // Different value is not equal.
            byte[] differentData = new byte[256];
            differentData[2] = 16;
            bloom2 = new Bloom(differentData);
            Assert.NotEqual(bloom1, bloom2);
        }

        [Fact]
        public void Length_Must_Be_Correct()
        {
            new Bloom(new byte[256]); // no exception
            Assert.Throws<ArgumentException>(() => new Bloom(new byte[0]));
            Assert.Throws<ArgumentException>(() => new Bloom(new byte[257]));
            Assert.Throws<ArgumentException>(() => new Bloom(null));
        }

        [Fact]
        public void Add_Items_To_Bloom_And_Test()
        {
            const string testValue1 = "095e7baea6a6c7c4c2dfeb977efac326af552d87";
            const string testValue2 = "0000000000000000000000000000000000000000000000000000000000000000";
            string expectedBloom = "00000000000000000000000000000000000000000000000000000040000000000020000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000004000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000080000000000000000000200000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000000000";

            var bloom = new Bloom();
            bloom.Add(testValue1.HexToByteArray());
            bloom.Add(testValue2.HexToByteArray());
            Assert.Equal(expectedBloom, (string) bloom.ToString());

            Assert.True((bool) bloom.Test(testValue1.HexToByteArray()));
            Assert.True((bool) bloom.Test(testValue2.HexToByteArray()));
            Assert.False((bool) bloom.Test("1001".HexToByteArray()));
            Assert.True((bool) bloom.Test(testValue1.HexToByteArray()));
        }

        [Fact]
        public void Test_Many_Bloom_Values()
        {
            // Hey friend! There's a miniscule chance that this test will fail purely due to chance.
            // If it happens to you, congratulations! You're one in a million.

            const int numberToGen = 25;
            const int byteLength = 32;

            var bloom = new Bloom();
            var random = new Random();
            List<byte[]> containedInBloom = new List<byte[]>();
            List<byte[]> notContainedInBloom = new List<byte[]>();

            // Fill bloom up with some random numbers.
            for (int i=0; i<numberToGen; i++)
            {
                byte[] buffer = new byte[byteLength];
                random.NextBytes(buffer);
                containedInBloom.Add(buffer);
                bloom.Add(buffer);
            }

            // Create some random numbers that aren't in the bloom.
            for(int i=0; i<numberToGen; i++)
            {
                byte[] buffer = new byte[byteLength];
                random.NextBytes(buffer);
                notContainedInBloom.Add(buffer);
            }

            // Check that all in bloom match, and all not in bloom don't match.
            for(int i = 0; i< numberToGen; i++)
            {
                Assert.False((bool) bloom.Test(notContainedInBloom[i]));
                Assert.True((bool) bloom.Test(containedInBloom[i]));
            }
        }

        [Fact]
        public void Bloom_Receipt_With_Logs()
        {
            var log1 = new Log(
                new uint160(12345),
                new List<byte[]>
                {
                    Encoding.UTF8.GetBytes("Topic1"),
                    Encoding.UTF8.GetBytes("Topic2")
                },
                null
            );

            var log2 = new Log(
                new uint160(123456),
                new List<byte[]>
                {
                    Encoding.UTF8.GetBytes("Topic3"),
                    Encoding.UTF8.GetBytes("Topic4")
                },
                null
            );

            var receipt = new Receipt(new uint256(0), 0, new Log[] { log1, log2 });

            Assert.True((bool) receipt.Bloom.Test(Encoding.UTF8.GetBytes("Topic1")));
            Assert.True((bool) receipt.Bloom.Test(Encoding.UTF8.GetBytes("Topic2")));
            Assert.True((bool) receipt.Bloom.Test(Encoding.UTF8.GetBytes("Topic3")));
            Assert.True((bool) receipt.Bloom.Test(Encoding.UTF8.GetBytes("Topic4")));
            Assert.True((bool) receipt.Bloom.Test(new uint160(12345).ToBytes()));
            Assert.True((bool) receipt.Bloom.Test(new uint160(123456).ToBytes()));

            Assert.False((bool) receipt.Bloom.Test(Encoding.UTF8.GetBytes("Topic5")));
            Assert.False((bool) receipt.Bloom.Test(Encoding.UTF8.GetBytes("Topic6")));
            Assert.False((bool) receipt.Bloom.Test(Encoding.UTF8.GetBytes("Topic7")));
            Assert.False((bool) receipt.Bloom.Test(Encoding.UTF8.GetBytes("Topic8")));
            Assert.False((bool) receipt.Bloom.Test(new uint160(11111).ToBytes()));
            Assert.False((bool) receipt.Bloom.Test(new uint160(1234567).ToBytes()));
        }
    }
}
