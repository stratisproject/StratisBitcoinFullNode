using System;
using System.Linq;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// TODO: Give the user a warning about using this or the array in any non-storage locations.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class SmartContractMapping<K, V>
    {
        private readonly uint _baseNumber;

        private byte[] BaseNumberBytes
        {
            get
            {
                return BitConverter.GetBytes(_baseNumber);
            }
        }

        internal SmartContractMapping(uint baseNum)
        {
            _baseNumber = baseNum;
        }

        public void Put(K key, V value)
        {
            PersistentState.SetObject(GetKeyBytes(key), value);
        }

        public V Get(K key)
        {
            return PersistentState.GetObject<V>(GetKeyBytes(key));
        }

        public V this[K key]
        {
            get
            {
                return Get(key);
            }
            set
            {
                Put(key, value);
            }
        }

        /// <summary>
        ///  This is so dumb. TODO:  We need to explicitly declare what happens for each of the allowed types. Same for putting the items into the db
        /// </summary>
        /// <param name="key"></param>
        private byte[] GetKeyBytes(K key)
        {
            var keyBytes = new byte[0];
            if (key is uint)
            {
                keyBytes = BaseNumberBytes.Concat(BitConverter.GetBytes((uint)(object)key)).ToArray();
            }
            else if (key is Address)
            {
                keyBytes = BaseNumberBytes.Concat(((Address)(object)key).ToUint160().ToBytes()).ToArray();
            }
            return keyBytes;
        }
    }
}
