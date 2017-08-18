using Stratis.Bitcoin.Utilities;
using System.Net;
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
            CircularArray<int> carray = new CircularArray<int>(8, false);

            carray.Add(10);
            carray.Add(20);
            carray.Add(30);
            carray.Add(40);
            carray.Add(50);
            carray.Add(60);

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
            CircularArray<int> carray = new CircularArray<int>(5, false);

            carray.Add(10);
            carray.Add(20);
            carray.Add(30);
            carray.Add(40);
            carray.Add(50);
            carray.Add(60);

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
            int[] array = new int[] { 7, 5, 6 };
            CircularArray<int> carray = new CircularArray<int>(5, false);

            // Add all items from normal array to circular array.
            for (int i = 0; i < array.Length; i++)
                carray.Add(array[i]);

            // Create a new array from enumeration over circular array.
            int index = 0;
            int[] arrayCopy = new int[array.Length];
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
            int[] array = new int[] { 7, 5, 6, 12, 1, 18, 8 };
            CircularArray<int> carray = new CircularArray<int>(5, false);

            // Add all items from normal array to circular array.
            for (int i = 0; i < array.Length; i++)
                carray.Add(array[i]);

            // Create a new array from enumeration over circular array.
            int index = 0;
            int[] arrayCopy = new int[carray.Capacity];
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
        public void AddNoSetWorksCorrectly()
        {
            CircularArray<IPHostEntry> carray = new CircularArray<IPHostEntry>(5);

            int index = carray.AddNoSet();
            carray[index].HostName = "a";

            index = carray.AddNoSet();
            carray[index].HostName = "b";

            index = carray.AddNoSet();
            carray[index].HostName = "c";

            string nameSum = "";
            foreach (IPHostEntry item in carray)
                nameSum += item.HostName;

            Assert.Equal("abc", nameSum);

            index = carray.AddNoSet();
            carray[index].HostName = "d";

            index = carray.AddNoSet();
            carray[index].HostName = "e";

            index = carray.AddNoSet();
            carray[index].HostName = "f";

            index = carray.AddNoSet();
            carray[index].HostName = "g";

            nameSum = "";
            foreach (IPHostEntry item in carray)
                nameSum += item.HostName;

            Assert.Equal("cdefg", nameSum);

            index = carray.AddNoSet();
            Assert.Equal("c", carray[index].HostName);
        }
    }
}
