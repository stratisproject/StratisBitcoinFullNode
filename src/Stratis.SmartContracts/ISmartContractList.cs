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
        /// <param name="item"></param>
        void Add(T item);

        /// <summary>
        /// Return the item at the given index in the list.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        T Get(uint index);

        /// <summary>
        /// Get the enumerator for the list.
        /// </summary>
        /// <returns></returns>
        IEnumerator<T> GetEnumerator();
    }
}