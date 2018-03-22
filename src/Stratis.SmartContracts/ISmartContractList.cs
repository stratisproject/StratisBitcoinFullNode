using System.Collections.Generic;

namespace Stratis.SmartContracts
{
    public interface ISmartContractList<T>
    {
        /// <summary>
        /// The length of the list is stored in the hash of the List name.Count
        /// </summary>
        uint Count { get; }
        void Add(T item);
        T Get(uint index);
        IEnumerator<T> GetEnumerator();
    }
}