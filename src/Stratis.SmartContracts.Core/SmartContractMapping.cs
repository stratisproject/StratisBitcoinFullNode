using System;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Should be used by smart contract devs for storing dictionary-like data structures.
    /// Stores and loads to KV store so is much more efficient than using a Dictionary.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class SmartContractMapping<V> : ISmartContractMapping<V>
    {
        private readonly StringKeyHashingStrategy keyHashingStrategy;
        private readonly string name;
        private readonly IPersistentState persistentState;

        public SmartContractMapping(IPersistentState persistentState, string name)
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
            this.persistentState.SetObject(this.GetKeyString(key), value);
        }

        public V Get(string key)
        {
            return this.persistentState.GetObject<V>(this.GetKeyString(key));
        }

        public V this[string key]
        {
            get
            {
                return this.Get(key);
            }
            set
            {
                this.Put(key, value);
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
