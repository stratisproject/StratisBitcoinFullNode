using System;
using System.Collections;
using System.Collections.Generic;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// This will be used by smart contract devs to manage lists of data. 
    /// They shouldn't use standard dictionaries, lists or arrays because they are not stored in the KV store,
    /// and so are completely deserialized or serialized every time. Very inefficient. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SmartContractList<T> : IEnumerable<T>, ISmartContractList<T>
    {
        private readonly IPersistentState persistentState;
        private readonly string name;
        private string CountName => $"{this.name}.Count";

        /// <summary>
        /// The length of the list is stored in the hash of the List name.Count
        /// </summary>
        public uint Count
        {
            get
            {
                string baseKey = this.KeyHashingStrategy.Hash(this.CountName);
                return this.persistentState.GetObject<uint>(baseKey);
            }
            private set
            {
                string baseKey = this.KeyHashingStrategy.Hash(this.CountName);
                this.persistentState.SetObject(baseKey, value);
            }
        }

        public SmartContractList(IPersistentState persistentState, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("SmartContractList name cannot be empty", nameof(name));
            }

            this.persistentState = persistentState;
            this.name = name;
            this.KeyHashingStrategy = StringKeyHashingStrategy.Default;
        }

        public StringKeyHashingStrategy KeyHashingStrategy { get; }

        public void Add(T item)
        {
            this.persistentState.SetObject(this.GetKeyString(this.FormatIndex(this.Count)), item);
            this.Count = this.Count + 1;
        }

        public T Get(uint index)
        {
            return this.persistentState.GetObject<T>(this.GetKeyString(this.FormatIndex(index)));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (uint i = 0; i < this.Count; i++)
            {
                yield return this.Get(i);
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
            return this.KeyHashingStrategy.Hash(
                this.name,
                key);
        }
    }
}
