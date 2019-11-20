using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class BloomExtensionsTests
    {
        [Fact]
        public void Test_Returns_True_When_Address_Null_Topics_Null()
        {
            var data = new byte[] {0xAA, 0xBB, 0xCC};

            var bloom = new Bloom();
            bloom.Add(data);

            Assert.True(bloom.Test((uint160)null, (IEnumerable<byte[]>)null));
        }

        [Fact]
        public void Test_Returns_True_When_Address_Not_Null_Topics_Null()
        {
            var address = new uint160(1);

            var bloom = new Bloom();
            bloom.Add(address.ToBytes());

            Assert.True(bloom.Test(address, null));
        }

        [Fact]
        public void Test_Returns_True_When_Address_Null_Topics_Not_Null()
        {
            var address = new uint160(1);

            var topics = new List<byte[]>
            {
                new byte[] {0xAA, 0xAA, 0xAA},
                new byte[] {0xBB, 0xBB, 0xBB},
                new byte[] {0xCC, 0xCC, 0xCC}
            };

            var bloom = new Bloom();
            bloom.Add(address.ToBytes());

            foreach (var topic in topics)
            {
                bloom.Add(topic);
            }

            Assert.True(bloom.Test(topics));
            Assert.True(bloom.Test(new[] {topics[0]}));
            Assert.True(bloom.Test(new[] {topics[1]}));
            Assert.True(bloom.Test(new[] {topics[2]}));
        }

        [Fact]
        public void Test_Returns_True_When_Address_Not_Null_Topics_Not_Null()
        {
            var address = new uint160(1);

            var topics = new List<byte[]>
            {
                new byte[] {0xAA, 0xAA, 0xAA},
                new byte[] {0xBB, 0xBB, 0xBB},
                new byte[] {0xCC, 0xCC, 0xCC}
            };

            var bloom = new Bloom();
            bloom.Add(address.ToBytes());

            foreach (var topic in topics)
            {
                bloom.Add(topic);
            }

            Assert.True(bloom.Test(address, topics));
            Assert.True(bloom.Test(address, new[] {topics[0]}));
            Assert.True(bloom.Test(address, new[] {topics[1]}));
            Assert.True(bloom.Test(address, new[] {topics[2]}));
        }

        [Fact]
        public void Test_Returns_False_When_Address_Not_Null_Topics_Not_Null()
        {
            var address = new uint160(1);

            var topics = new List<byte[]>
            {
                new byte[] {0xAA, 0xAA, 0xAA},
                new byte[] {0xBB, 0xBB, 0xBB},
                new byte[] {0xCC, 0xCC, 0xCC}
            };

            var bloom = new Bloom();
            bloom.Add(address.ToBytes());

            foreach (var topic in topics)
            {
                bloom.Add(topic);
            }

            var address2 = new uint160(2);

            Assert.False(bloom.Test(address2));
            Assert.False(bloom.Test(address2, new[] {topics[0]}));
            Assert.False(bloom.Test(address2, new[] {topics[1]}));
            Assert.False(bloom.Test(address2, new[] {topics[2]}));
        }

        [Fact]
        public void Test_Does_Not_Modify_Subject()
        {
            var bloom = new Bloom();

            var bloomCopy = new Bloom(bloom.ToBytes().ToArray());

            bloom.Test(uint160.One, new [] { new byte[] {0xAA, 0xAA, 0xAA} });

            // Test that the internal data of the original bloom has not changed.
            Assert.Equal(bloom, bloomCopy);
        }
    }
}
