using System;
using System.Linq;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Should be used by smart contract devs for storing dictionary-like data structures.
    /// Stores and loads to KV store so is much more efficient than using a Dictionary.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class SmartContractMapping<K, V>
    {
        private readonly uint baseNumber;
        private readonly PersistentStateSerializer serializer = new PersistentStateSerializer();

        private byte[] BaseNumberBytes
        {
            get
            {
                return BitConverter.GetBytes(this.baseNumber);
            }
        }

        internal SmartContractMapping(uint baseNum)
        {
            this.baseNumber = baseNum;
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

        private byte[] GetKeyBytes(K key)
        {
            return this.BaseNumberBytes.Concat(this.serializer.Serialize(key)).ToArray();
        }
    }
}
