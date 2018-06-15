using System.Collections;
using System.Collections.Generic;

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
    /// <item><see cref="this[int]"/> - O(1).</item>
    /// </list>
    /// </remarks>
    public class CircularArray<T> : IEnumerable<T> where T : new()
    {
        /// <summary>Maximal number of items that can be stored in <see cref="items"/> array.</summary>
        public int Capacity { get; private set; }

        /// <summary>Number of valid (slots in array occupied by added items) items in <see cref="items"/> array.</summary>
        public int Count { get; private set; }

        /// <summary>Index in <see cref="items"/> array where the next item will be stored.</summary>
        public int Index { get; private set; }

        /// <summary>Circular array of items, which holds <see cref="Count"/> valid items, and can store up to <see cref="Capacity"/> items.</summary>
        private T[] items;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="capacity">Maximal number of items that can be stored in the circular array.</param>
        /// <param name="preinitializeItems">If set to <c>true</c>, all items in the array will be initialized using their default constructor.
        /// This can be used to prevent further allocations when the structure is used.</param>
        public CircularArray(int capacity, bool preinitializeItems = true)
        {
            this.Index = 0;
            this.Count = 0;
            this.Capacity = capacity;
            this.items = new T[capacity];
            if (preinitializeItems)
            {
                for (int i = 0; i < capacity; i++)
                    this.items[i] = new T();
            }
        }

        /// <summary>
        /// Add a new item to the circular array.
        /// </summary>
        /// <param name="item">Item to add.</param>
        /// <param name="oldItem">If the function returns <c>true</c>, this is filled with the oldest item that was replaced.</param>
        /// <returns><c>true</c> if the oldest item was replaced, <c>false</c> otherwise.</returns>
        /// <remarks>If the array already reached its capacity, this method will replace the oldest item with the new item.</remarks>
        public bool Add(T item, out T oldItem)
        {
            bool res = false;
            oldItem = default(T);
            if (this.Count < this.Capacity)
            {
                this.Count++;
            }
            else
            {
                oldItem = this.items[this.Index];
                res = true;
            }

            this.items[this.Index] = item;
            this.Index = (this.Index + 1) % this.Capacity;

            return res;
        }

        /// <summary>
        /// Removes the first item from the circular array, which is the oldest entry in the array.
        /// </summary>
        /// <param name="firstItem">If the function returns <c>true</c>, this is filled with the oldest item that was removed.</param>
        /// <returns><c>true</c> if the oldest item was removed, <c>false</c> if there were no items.</returns>
        public bool RemoveFirst(out T firstItem)
        {
            bool res = false;
            firstItem = default(T);
            if (this.Count > 0)
            {
                firstItem = this.items[this.Index];
                this.Index = (this.Index + 1) % this.Capacity;
                this.Count--;
                res = true;
            }

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
            if (this.Count == this.Capacity) itemIndex = this.Index;

            for (int i = 0; i < this.Count; i++)
            {
                yield return this.items[itemIndex];
                itemIndex = (itemIndex + 1) % this.Capacity;
            }
        }
    }
}
