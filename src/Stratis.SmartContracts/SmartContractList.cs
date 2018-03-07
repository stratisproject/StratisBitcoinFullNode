using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Hashing;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// This will be used by smart contract devs to manage lists of data. 
    /// They shouldn't use standard dictionaries, lists or arrays because they are not stored in the KV store,
    /// and so are completely deserialized or serialized every time. Very inefficient. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SmartContractList<T> : IEnumerable<T>
    {

        /// <summary>
        /// Used to differentiate between each mapping / list. Each one will have a unique baseNumber
        /// that will allow them to all save data to a different domain - Keccak(BaseNumberBytes + Key)
        /// </summary>
        private readonly uint baseNumber;

        private PersistentState persistentState;

        private byte[] BaseNumberBytes
        {
            get
            {
                return BitConverter.GetBytes(this.baseNumber);
            }
        }

        /// <summary>
        /// The length of the list is stored in the hash of the baseNumber
        /// </summary>
        public uint Count
        {
            get
            {
                string baseKey = Encoding.UTF8.GetString(HashHelper.Keccak256(this.BaseNumberBytes));
                return this.persistentState.GetObject<uint>(baseKey);
            }
            private set
            {
                string baseKey = Encoding.UTF8.GetString(HashHelper.Keccak256(this.BaseNumberBytes));
                this.persistentState.SetObject(baseKey, value);
            }
        }

        internal SmartContractList(PersistentState persistentState, uint baseNumber)
        {
            this.persistentState = persistentState;
            this.baseNumber = baseNumber;
        }

        public void Add(T item)
        {
            this.persistentState.SetObject(GetKeyString(this.Count), item);
            this.Count = this.Count + 1;
        }

        public T Get(uint index)
        {
            return this.persistentState.GetObject<T>(GetKeyString(index));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SmartContractListEnum<T>(this.persistentState, this.baseNumber, this.Count);
        }

        /// <summary>
        /// Gets the actual key to be used in persistentstate for each item in the list.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string GetKeyString(uint key)
        {
            byte[] toHash = BitConverter.GetBytes(this.baseNumber).Concat(Encoding.UTF8.GetBytes(key.ToString())).ToArray();
            return Encoding.UTF8.GetString(HashHelper.Keccak256(toHash));
        }

    }

    public class SmartContractListEnum<T> : IEnumerator<T>
    {
        private readonly uint baseNumber;
        private readonly uint length;
        private int position = -1;
        private PersistentState PersistentState;

        private byte[] BaseNumberBytes
        {
            get
            {
                return BitConverter.GetBytes(this.baseNumber);
            }
        }

        public T Current
        {
            get
            {
                return PersistentState.GetObject<T>(GetKeyString((uint)this.position));
            }
        }

        object IEnumerator.Current => this.Current;

        public SmartContractListEnum(PersistentState persistentState, uint baseNumber, uint length)
        {
            this.PersistentState = persistentState;
            this.baseNumber = baseNumber;
            this.length = length;
        }

        public void Dispose() {}

        public bool MoveNext()
        {
            this.position++;
            return (this.position < this.length);
        }

        public void Reset()
        {
            this.position = -1;
        }

        private string GetKeyString(uint key)
        {
            byte[] toHash = BitConverter.GetBytes(this.baseNumber).Concat(Encoding.UTF8.GetBytes(key.ToString())).ToArray();
            return Encoding.UTF8.GetString(HashHelper.Keccak256(toHash));
        }
    }
}
