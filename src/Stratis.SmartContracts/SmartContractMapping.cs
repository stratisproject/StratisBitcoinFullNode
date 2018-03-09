using System;
using System.Linq;
using System.Text;
using Stratis.SmartContracts.Hashing;

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
        private readonly uint baseNumber;
        private readonly PersistentState persistentState;

        internal SmartContractMapping(PersistentState persistentState, uint baseNum)
        {
            this.persistentState = persistentState;
            this.baseNumber = baseNum;
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
            byte[] toHash = BitConverter.GetBytes(this.baseNumber).Concat(Encoding.UTF8.GetBytes(key.ToString())).ToArray();
            return Encoding.UTF8.GetString(HashHelper.Keccak256(toHash));
        }
    }
}
