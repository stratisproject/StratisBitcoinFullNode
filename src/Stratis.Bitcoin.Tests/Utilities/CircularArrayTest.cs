using System.Net;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    /// <summary>
    /// Tests of <see cref="CircularArray{T}"/> class.
    /// </summary>
    public class CircularArrayTest
    {
        /// <summary>
        /// Checks that adding new items to the circular array as well as item enumeration works correctly.
        /// This test uses less items than the capacity of the array.
        /// </summary>
        [Fact]
        public void AddAndEnumerationWithLessThanCapacityItemsWorksCorrectly()
        {
            var carray = new CircularArray<int>(8, false);

            carray.Add(10, out int oldItem);
            carray.Add(20, out oldItem);
            carray.Add(30, out oldItem);
            carray.Add(40, out oldItem);
            carray.Add(50, out oldItem);
            carray.Add(60, out oldItem);

            int sum = 0;
            foreach (int item in carray)
                sum += item;

            Assert.Equal(10 + 20 + 30 + 40 + 50 + 60, sum);
        }

        /// <summary>
        /// Checks that adding new items to the circular array as well as item enumeration works correctly.
        /// This test uses more items than the capacity of the array.
        /// </summary>
        [Fact]
        public void AddAndEnumerationWithMoreThanCapacityItemsWorksCorrectly()
        {
            var carray = new CircularArray<int>(5, false);

            carray.Add(10, out int oldItem);
            carray.Add(20, out oldItem);
            carray.Add(30, out oldItem);
            carray.Add(40, out oldItem);
            carray.Add(50, out oldItem);
            carray.Add(60, out oldItem);

            int sum = 0;
            foreach (int item in carray)
                sum += item;

            Assert.Equal(20 + 30 + 40 + 50 + 60, sum);
        }

        /// <summary>
        /// Checks that enumeration of items is in correct order.
        /// This test uses less items than the capacity of the array.
        /// </summary>
        [Fact]
        public void EnumerationOrderWithLessThanCapacityItemsIsCorrect()
        {
            var array = new int[] { 7, 5, 6 };
            var carray = new CircularArray<int>(5, false);

            // Add all items from normal array to circular array.
            for (int i = 0; i < array.Length; i++)
                carray.Add(array[i], out int oldItem);

            // Create a new array from enumeration over circular array.
            int index = 0;
            var arrayCopy = new int[array.Length];
            foreach (int item in carray)
                arrayCopy[index++] = item;

            // Check that each item from the enumeration was in correct order.
            for (int i = 0; i < array.Length; i++)
                Assert.Equal(array[i], arrayCopy[i]);
        }

        /// <summary>
        /// Checks that enumeration of items is in correct order.
        /// This test uses more items than the capacity of the array.
        /// </summary>
        [Fact]
        public void EnumerationOrderMoreThanCapacityItemsIsCorrect()
        {
            var array = new int[] { 7, 5, 6, 12, 1, 18, 8 };
            var carray = new CircularArray<int>(5, false);

            // Add all items from normal array to circular array.
            for (int i = 0; i < array.Length; i++)
                carray.Add(array[i], out int oldItem);

            // Create a new array from enumeration over circular array.
            int index = 0;
            var arrayCopy = new int[carray.Capacity];
            foreach (int item in carray)
                arrayCopy[index++] = item;

            // Check that each item from the enumeration was in correct order.
            for (int i = 0; i < carray.Capacity; i++)
                Assert.Equal(array[array.Length - carray.Capacity + i], arrayCopy[i]);
        }

        /// <summary>
        /// Checks that adding new item without setting the value works as expected.
        /// </summary>
        [Fact]
        public void AddReplacementsWorkCorrectly()
        {
            var carray = new CircularArray<IPHostEntry>(5);

            IPHostEntry oldItem;
            bool replaced = carray.Add(new IPHostEntry { HostName = "a" }, out oldItem);
            Assert.False(replaced);

            replaced = carray.Add(new IPHostEntry { HostName = "b" }, out oldItem);
            Assert.False(replaced);

            replaced = carray.Add(new IPHostEntry { HostName = "c" }, out oldItem);
            Assert.False(replaced);

            string nameSum = "";
            foreach (IPHostEntry item in carray)
                nameSum += item.HostName;

            Assert.Equal("abc", nameSum);

            replaced = carray.Add(new IPHostEntry { HostName = "d" }, out oldItem);
            Assert.False(replaced);

            replaced = carray.Add(new IPHostEntry { HostName = "e" }, out oldItem);
            Assert.False(replaced);

            replaced = carray.Add(new IPHostEntry { HostName = "f" }, out oldItem);
            Assert.True(replaced);

            replaced = carray.Add(new IPHostEntry { HostName = "g" }, out oldItem);
            Assert.True(replaced);

            nameSum = "";
            foreach (IPHostEntry item in carray)
                nameSum += item.HostName;

            Assert.Equal("cdefg", nameSum);

            replaced = carray.Add(new IPHostEntry { HostName = "g" }, out oldItem);
            Assert.True(replaced);
            Assert.Equal("c", oldItem.HostName);
        }
    }
}
