using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Generic circular array is a fixed length array which stores collection 
    /// of items. Once the array is full, adding a new item removes the oldest entry. 
    /// </summary>
    /// <typeparam name="T">Type of the items stored in the array.</typeparam>
    /// <remarks>
    /// Complexity of supported operations:
    /// <list type="bullet">
    /// <item><see cref="Add"/> - O(1),</item>
    /// <item><see cref="AddNoSet"/> - O(1),</item>
    /// <item><see cref="this[int]"/> - O(1).</item>
    /// </list>
    /// </remarks>
    public class CircularArray<T> : IEnumerable<T> where T : new()
    {
        /// <summary>Maximal number of items that can be stored in <see cref="items"/> array.</summary>
        public int Capacity { get; private set; }

        /// <summary>Number of valid items in <see cref="items"/> array.</summary>
        public int Count { get; private set; }

        /// <summary>Index in <see cref="items"/> array where the next item will be stored.</summary>
        public int Index { get; private set; }

        /// <summary>Circular array of items, which holds <see cref="Count"/> valid items, and can store up to <see cref="Capacity"/> items.</summary>
        private T[] items;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="capacity">Maximal number of items that can be stored in the circular array.</param>
        /// <param name="initializeItems">If set to <c>true</c>, all items in the array will be initialized using their default constructor. 
        /// This can be used to prevent further allocations when the structure is used.</param>
        public CircularArray(int capacity, bool initializeItems = true)
        {
            this.Index = 0;
            this.Count = 0;
            this.Capacity = capacity;
            this.items = new T[capacity];
            if (initializeItems)
            {
                for (int i = 0; i < capacity; i++)
                    this.items[i] = new T();
            }
        }

        /// <summary>
        /// Add a new item to the circular array.
        /// </summary>
        /// <param name="item">Item to add.</param>
        /// <remarks>If the array already reached its capacity, this method will replace the oldest item with the new item.</remarks>
        public void Add(T item)
        {
            int index = this.AddNoSet();
            this.items[index] = item;
        }

        /// <summary>
        /// Add a new item to the circular array without setting its value.
        /// </summary>
        /// <returns>Index to the array where the new item should be placed.</returns>
        /// <remarks>This method only moves the array indexes as if a new item was added, but the caller is responsible for actually setting new values of the item.</remarks>
        public int AddNoSet()
        {
            if (this.Count < this.Capacity)
                this.Count++;

            int res = this.Index;
            this.Index = (this.Index + 1) % this.Capacity;

            return res;
        }

        /// <summary>
        /// Access to an item at specific index.
        /// </summary>
        /// <param name="i">Zero-based index of the item to access. Index must be an integer between 0 and <see cref="Capacity"/> - 1.</param>
        /// <returns>Item of the circular array at index <paramref name="i"/>.</returns>
        public T this[int i]
        {
            get { return this.items[i]; }
            set { this.items[i] = value; }
        }
        
        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <inheritdoc />
        /// <remarks>Items are enumerated in order of their addition starting with the oldest item.</remarks>
        public IEnumerator<T> GetEnumerator()
        {
            int itemIndex = 0;
            if (this.Count == this.Capacity) itemIndex = (this.Index + 1) % this.Capacity;

            for (int i = 0; i < this.Count; i++)
            {
                yield return this.items[itemIndex];
                itemIndex = (itemIndex + 1) % this.Capacity;
            }
        }
    }
}
