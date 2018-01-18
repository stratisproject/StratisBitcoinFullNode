using Stratis.SmartContracts.Hashing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.SmartContracts
{
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
            var keyBytes = HashHelper.Keccak256(GetKeyBytes(Count));
            PersistentState.SetObject(keyBytes, item);
            Count = Count + 1;
        }

        public T Get(uint index)
        {
            var keyBytes = HashHelper.Keccak256(GetKeyBytes(index));
            return PersistentState.GetObject<T>(keyBytes);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SmartContractListEnum<T>(this.baseNumber, Count);
        }

        private byte[] GetKeyBytes(uint key)
        {
            return BaseNumberBytes.Concat(new uint256(key).ToBytes()).ToArray();
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
                var keyBytes = HashHelper.Keccak256(GetKeyBytes(Convert.ToUInt32(this.position)));
                return PersistentState.GetObject<T>(keyBytes);
            }
        }

        object IEnumerator.Current => Current;

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
