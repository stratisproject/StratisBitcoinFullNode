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
        private readonly uint _baseNumber;

        private byte[] BaseNumberBytes
        {
            get
            {
                return BitConverter.GetBytes(_baseNumber);
            }
        }

        public uint Count
        {
            get
            {
                return PersistentState.GetObject<uint>(BaseNumberBytes);
            }
            private set
            {
                PersistentState.SetObject(BaseNumberBytes, value);
            }
        }

        internal SmartContractList(uint baseNumber)
        {
            _baseNumber = baseNumber;
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
            return new SmartContractListEnum<T>(_baseNumber, Count);
        }

        private byte[] GetKeyBytes(uint key)
        {
            return BaseNumberBytes.Concat(new uint256(key).ToBytes()).ToArray();
        }
    }

    public class SmartContractListEnum<T> : IEnumerator<T>
    {
        private readonly uint _baseNumber;
        private readonly uint _length;
        private int _position = -1;

        private byte[] BaseNumberBytes
        {
            get
            {
                return BitConverter.GetBytes(_baseNumber);
            }
        }

        public T Current
        {
            get
            {
                var keyBytes = HashHelper.Keccak256(GetKeyBytes(Convert.ToUInt32(_position)));
                return PersistentState.GetObject<T>(keyBytes);
            }
        }

        object IEnumerator.Current => Current;

        public SmartContractListEnum(uint baseNumber, uint length)
        {
            _baseNumber = baseNumber;
            _length = length;
        }

        public void Dispose() {}

        public bool MoveNext()
        {
            _position++;
            return (_position < _length);
        }

        public void Reset()
        {
            _position = -1;
        }

        private byte[] GetKeyBytes(uint key)
        {
            return BaseNumberBytes.Concat(BitConverter.GetBytes(key)).ToArray();
        }
    }
}
