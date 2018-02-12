using Stratis.SmartContracts.Hashing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

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
        private readonly uint baseNumber;

        private byte[] BaseNumberBytes
        {
            get
            {
                return BitConverter.GetBytes(this.baseNumber);
            }
        }

        public uint Count
        {
            get
            {
                return PersistentState.GetObject<uint>(this.BaseNumberBytes);
            }
            private set
            {
                PersistentState.SetObject(this.BaseNumberBytes, value);
            }
        }

        internal SmartContractList(uint baseNumber)
        {
            this.baseNumber = baseNumber;
        }

        public void Add(T item)
        {
            byte[] keyBytes = HashHelper.Keccak256(GetKeyBytes(this.Count));
            PersistentState.SetObject(keyBytes, item);
            this.Count = this.Count + 1;
        }

        public T Get(uint index)
        {
            byte[] keyBytes = HashHelper.Keccak256(GetKeyBytes(index));
            return PersistentState.GetObject<T>(keyBytes);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SmartContractListEnum<T>(this.baseNumber, this.Count);
        }

        private byte[] GetKeyBytes(uint key)
        {
            return this.BaseNumberBytes.Concat(new uint256(key).ToBytes()).ToArray();
        }
    }

    public class SmartContractListEnum<T> : IEnumerator<T>
    {
        private readonly uint baseNumber;
        private readonly uint length;
        private int position = -1;

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
                byte[] keyBytes = HashHelper.Keccak256(GetKeyBytes(Convert.ToUInt32(this.position)));
                return PersistentState.GetObject<T>(keyBytes);
            }
        }

        object IEnumerator.Current => this.Current;

        public SmartContractListEnum(uint baseNumber, uint length)
        {
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

        private byte[] GetKeyBytes(uint key)
        {
            return this.BaseNumberBytes.Concat(BitConverter.GetBytes(key)).ToArray();
        }
    }
}
