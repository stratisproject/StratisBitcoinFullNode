using System;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Should be used by smart contract devs for storing dictionary-like data structures.
    /// Stores and loads to KV store so is much more efficient than using a Dictionary.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class SmartContractMapping<V> : ISmartContractMapping<V>
    {
        private readonly string name;
        private readonly PersistentState persistentState;

        public SmartContractMapping(PersistentState persistentState, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("SmartContractMapping name cannot be empty", nameof(name));
            }

            this.name = name;
            this.persistentState = persistentState;
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

        private string GetKeyString(string key)
        {
            return this.name + FormatKey(key);
        }

        private string FormatKey(string key)
        {
            return $"[{key}]";
        }
    }
}
