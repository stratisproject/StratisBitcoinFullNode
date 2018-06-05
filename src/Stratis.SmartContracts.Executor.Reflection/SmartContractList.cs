using System;
using System.Collections;
using System.Collections.Generic;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// This will be used by smart contract devs to manage lists of data. 
    /// They shouldn't use standard dictionaries, lists or arrays because they are not stored in the KV store,
    /// and so are completely deserialized or serialized every time. Very inefficient. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SmartContractList<T> : IEnumerable<T>, ISmartContractList<T>
    {
        private readonly PersistentState persistentState;
        private readonly string name;
        private string CountName => $"{this.name}.Count";

        /// <summary>
        /// The length of the list is stored in the hash of the List name.Count
        /// </summary>
        public uint Count
        {
            get
            {
                return this.persistentState.GetUInt32(this.CountName);
            }
            private set
            {
                this.persistentState.SetUInt32(this.CountName, value);
            }
        }

        public T this[uint index]
        {
            get
            {
                return GetValue(index);
            }
            set
            {
                SetValue(index, value);
            }

        }

        public SmartContractList(PersistentState persistentState, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("SmartContractList name cannot be empty", nameof(name));
            }

            this.persistentState = persistentState;
            this.name = name;
        }

        public void Add(T item)
        {
            SetValue(this.Count, item);
            this.Count = this.Count + 1;
        }

        public T GetValue(uint index)
        {
            return this.persistentState.GetObject<T>(this.GetKeyString(this.FormatIndex(index)));
        }

        public void SetValue(uint index, T value)
        {
            this.persistentState.SetObject(this.GetKeyString(this.FormatIndex(index)), value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (uint i = 0; i < this.Count; i++)
            {
                yield return this.GetValue(i);
            }
        }

        private string FormatIndex(uint index)
        {
            return $"[{index}]";
        }

        /// <summary>
        /// Gets the actual key to be used in persistentstate for each item in the list.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string GetKeyString(string key)
        {
            return this.name + key; 
        }
    }
}
