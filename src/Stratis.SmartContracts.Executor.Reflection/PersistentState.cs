using System;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class PersistentState : IPersistentState
    {
        public uint160 ContractAddress { get; }
        private readonly IPersistenceStrategy persistenceStrategy;
        private readonly Network network;

        /// <summary>
        /// Instantiate a new PersistentState instance. Each PersistentState object represents
        /// a slice of state for a particular contract address.
        /// </summary>
        public PersistentState(
            IPersistenceStrategy persistenceStrategy,
            IContractPrimitiveSerializer contractPrimitiveSerializer,
            uint160 contractAddress)
        {
            this.persistenceStrategy = persistenceStrategy;
            this.Serializer = contractPrimitiveSerializer;
            this.ContractAddress = contractAddress;
        }

        internal IContractPrimitiveSerializer Serializer { get; }

        internal T GetObject<T>(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] bytes = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);

            if (bytes == null)
                return default(T);

            return this.Serializer.Deserialize<T>(bytes);
        }

        public byte[] GetBytes(string key)
        {
            return this.GetObject<byte[]>(key);
        }

        public char GetChar(string key)
        {
            return this.GetObject<char>(key);
        }

        public Address GetAddress(string key)
        {
            return this.GetObject<Address>(key);
        }

        public bool GetBool(string key)
        {
            return this.GetObject<bool>(key);
        }

        public int GetInt32(string key)
        {
            return this.GetObject<int>(key);
        }

        public uint GetUInt32(string key)
        {
            return this.GetObject<uint>(key);
        }

        public long GetInt64(string key)
        {
            return this.GetObject<long>(key);
        }

        public ulong GetUInt64(string key)
        {
            return this.GetObject<ulong>(key);
        }

        public string GetString(string key)
        {
            return this.GetObject<string>(key);
        }

        public T GetStruct<T>(string key) where T : struct
        {
            return this.GetObject<T>(key);
        }

        public T[] GetArray<T>(string key)
        {
            return this.GetObject<T[]>(key);
        }

        internal void SetObject<T>(string key, T obj)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            this.persistenceStrategy.StoreBytes(this.ContractAddress, keyBytes, this.Serializer.Serialize(obj));
        }

        public void SetBytes(string key, byte[] value)
        {
            this.SetObject(key, value);
        }

        public void SetChar(string key, char value)
        {
            this.SetObject(key, value);
        }

        public void SetAddress(string key, Address value)
        {
            this.SetObject(key, value);
        }

        public void SetBool(string key, bool value)
        {
            this.SetObject(key, value);
        }

        public void SetInt32(string key, int value)
        {
            this.SetObject(key, value);
        }

        public void SetUInt32(string key, uint value)
        {
            this.SetObject(key, value);
        }

        public void SetInt64(string key, long value)
        {
            this.SetObject(key, value);
        }

        public void SetUInt64(string key, ulong value)
        {
            this.SetObject(key, value);
        }

        public void SetString(string key, string value)
        {
            this.SetObject(key, value);
        }

        public void SetStruct<T>(string key, T value) where T : struct
        {
            this.SetObject(key, value);
        }

        public void SetArray(string key, Array a)
        {
            this.SetObject(key, a);
        }
    }
}