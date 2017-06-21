using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stratis.Bitcoin.Tests.Utilities
{
    [TestClass]
    public class LinqExtensionsTest
    {
        [TestMethod]
        public void EmptySetTest()
        {
            List<long> list = new List<long> { };

            var median = list.Median();

            Assert.AreEqual(0, median);
        }

        [TestMethod]
        public void SingleSetTest()
        {
            List<long> list = new List<long> { 22 };

            var median = list.Median();

            Assert.AreEqual(22, median);
        }

        [TestMethod]
        public void OddSetTest()
        {
            List<long> list = new List<long> { 0, 1, 4, 8, 9, 12 };

            var median = list.Median();

            Assert.AreEqual(6, median);
        }

        [TestMethod]
        public void EvenSetTest()
        {
            List<long> list = new List<long> { 1, 4, 8, 9, 12 };

            var median = list.Median();

            Assert.AreEqual(8, median);
        }

        [TestMethod]
        public void UnorderedSetTest()
        {
            List<long> list = new List<long> { 12, 0, 1, 4, 8, 9 };

            var median = list.Median();

            Assert.AreEqual(6, median);
        }
    }
}
