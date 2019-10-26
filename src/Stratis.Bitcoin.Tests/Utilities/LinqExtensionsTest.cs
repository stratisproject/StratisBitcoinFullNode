using System.Collections.Generic;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class LinqExtensionsTest
    {
        [Fact]
        public void EmptySetTest()
        {
            var list = new List<long>();

            long median = list.Median();

            Assert.Equal(0, median);
        }

        [Fact]
        public void SingleSetTest()
        {
            var list = new List<long> { 22 };

            long median = list.Median();

            Assert.Equal(22, median);
        }

        [Fact]
        public void OddSetTest()
        {
            var list = new List<long> { 0, 1, 4, 8, 9, 12 };

            long median = list.Median();

            Assert.Equal(6, median);
        }

        [Fact]
        public void EvenSetTest()
        {
            var list = new List<long> { 1, 4, 8, 9, 12 };

            long median = list.Median();

            Assert.Equal(8, median);
        }

        [Fact]
        public void UnorderedSetTest()
        {
            var list = new List<long> { 12, 0, 1, 4, 8, 9 };

            long median = list.Median();

            Assert.Equal(6, median);
        }
    }
}
