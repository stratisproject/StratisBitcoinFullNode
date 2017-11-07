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
            List<long> list = new List<long>();

            var median = list.Median();

            Assert.Equal(0, median);
        }

        [Fact]
        public void SingleSetTest()
        {
            List<long> list = new List<long> { 22 };

            var median = list.Median();

            Assert.Equal(22, median);
        }

        [Fact]
        public void OddSetTest()
        {
            List<long> list = new List<long> { 0, 1, 4, 8, 9, 12 };

            var median = list.Median();

            Assert.Equal(6, median);
        }

        [Fact]
        public void EvenSetTest()
        {
            List<long> list = new List<long> { 1, 4, 8, 9, 12 };

            var median = list.Median();

            Assert.Equal(8, median);
        }

        [Fact]
        public void UnorderedSetTest()
        {
            List<long> list = new List<long> { 12, 0, 1, 4, 8, 9 };

            var median = list.Median();

            Assert.Equal(6, median);
        }


    }
}
