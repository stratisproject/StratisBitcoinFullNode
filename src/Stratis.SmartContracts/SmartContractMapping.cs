using System;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Should be used by smart contract devs for storing dictionary-like data structures.
    /// Stores and loads to KV store so is much more efficient than using a Dictionary.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class SmartContractMapping<V>
    {
        private readonly StringKeyHashingStrategy keyHashingStrategy;
        private readonly string name;
        private readonly PersistentState persistentState;

        internal SmartContractMapping(PersistentState persistentState, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("SmartContractMapping name cannot be empty", nameof(name));
            }

            this.name = name;
            this.persistentState = persistentState;
            this.keyHashingStrategy = StringKeyHashingStrategy.Default;
        }

        public void Put(string key, V value)
        {
            this.persistentState.SetObject(GetKeyString(key), value);
        }

        public V Get(string key)
        {
            return this.persistentState.GetObject<V>(GetKeyString(key));
        }

        public V this[string key]
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
        /// I feel like there's a better way of doing this. If someone stores a value in keccak256("{baseNum}{stringKey}"), it overwrites it!
        /// 
        /// Can we somehow handle mappings + lists in a way that people can't mess with them?
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string GetKeyString(string key)
        {
            return this.keyHashingStrategy.Hash(this.name, key);
        }
    }
}
