using System.Collections.Generic;

namespace Stratis.SmartContracts
{
    public interface ISmartContractList<T>
    {
        /// <summary>
        /// The number of items contained in the list.
        /// </summary>
        uint Count { get; }

        /// <summary>
        /// Add an item to the end of the list.
        /// </summary>
        void Add(T item);

        /// <summary>
        /// Return the item at the given index in the list.
        /// </summary>
        T GetValue(uint index);

        void SetValue(uint index, T value);

        /// <summary>
        /// Get the enumerator for the list.
        /// </summary>
        IEnumerator<T> GetEnumerator();

        T this[uint key] { get; set; }
    }
}