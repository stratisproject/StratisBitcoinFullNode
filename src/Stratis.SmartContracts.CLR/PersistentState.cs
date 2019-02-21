using System;
using NBitcoin;

namespace Stratis.SmartContracts.CLR
{
    public class PersistentState : IPersistentState
    {
        public uint160 ContractAddress { get; }
        private readonly IPersistenceStrategy persistenceStrategy;

        /// <summary>
        /// Instantiate a new PersistentState instance. Each PersistentState object represents
        /// a slice of state for a particular contract address.
        /// </summary>
        public PersistentState(IPersistenceStrategy persistenceStrategy,
            ISerializer serializer,
            uint160 contractAddress)
        {
            this.persistenceStrategy = persistenceStrategy;
            this.Serializer = serializer;
            this.ContractAddress = contractAddress;
        }

        internal ISerializer Serializer { get; }

        public bool IsContract(Address address)
        {
            byte[] serialized = this.Serializer.Serialize(address);

            if (serialized == null)
            {
                return false;
            }

            var contractAddress = new uint160(serialized);

            return this.persistenceStrategy.ContractExists(contractAddress);
        }

        public byte[] GetBytes(byte[] key)
        {
            byte[] bytes = this.persistenceStrategy.FetchBytes(this.ContractAddress, key);

            if (bytes == null)
                return new byte[0];

            return bytes;
        }

        public byte[] GetBytes(string key)
        {
            byte[] keyBytes = this.Serializer.Serialize(key);

            return this.GetBytes(keyBytes);
        }

        public char GetChar(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.Serializer.ToChar(bytes);
        }

        public Address GetAddress(string key)
        {
            byte[] bytes = this.GetBytes(key);
            
            return this.Serializer.ToAddress(bytes);
        }

        public bool GetBool(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.Serializer.ToBool(bytes);
        }

        public int GetInt32(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.Serializer.ToInt32(bytes);
        }

        public uint GetUInt32(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.Serializer.ToUInt32(bytes);
        }

        public long GetInt64(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.Serializer.ToInt64(bytes);
        }

        public ulong GetUInt64(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.Serializer.ToUInt64(bytes);
        }

        public string GetString(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.Serializer.ToString(bytes);
        }

        public T GetStruct<T>(string key) where T : struct
        {
            byte[] bytes = this.GetBytes(key);

            return this.Serializer.ToStruct<T>(bytes);
        }

        public T[] GetArray<T>(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.Serializer.ToArray<T>(bytes);
        }

        public void SetBytes(byte[] key, byte[] value)
        {
            this.persistenceStrategy.StoreBytes(this.ContractAddress, key, value);
        }

        public void SetBytes(string key, byte[] value)
        {
            byte[] keyBytes = this.Serializer.Serialize(key);

            this.SetBytes(keyBytes, value);
        }

        public void SetChar(string key, char value)
        {
            this.SetBytes(key, this.Serializer.Serialize(value));
        }

        public void SetAddress(string key, Address value)
        {
            this.SetBytes(key, this.Serializer.Serialize(value));
        }

        public void SetBool(string key, bool value)
        {
            this.SetBytes(key, this.Serializer.Serialize(value));
        }

        public void SetInt32(string key, int value)
        {
            this.SetBytes(key, this.Serializer.Serialize(value));
        }

        public void SetUInt32(string key, uint value)
        {
            this.SetBytes(key, this.Serializer.Serialize(value));
        }

        public void SetInt64(string key, long value)
        {
            this.SetBytes(key, this.Serializer.Serialize(value));
        }

        public void SetUInt64(string key, ulong value)
        {
            this.SetBytes(key, this.Serializer.Serialize(value));
        }

        public void SetString(string key, string value)
        {
            this.SetBytes(key, this.Serializer.Serialize(value));
        }

        public void SetArray(string key, Array a)
        {
            this.SetBytes(key, this.Serializer.Serialize(a));
        }

        public void SetStruct<T>(string key, T value) where T : struct
        {
            this.SetBytes(key, this.SerializeStruct(value));
        }

        private byte[] SerializeStruct<T>(T value) where T : struct
        {
            return this.Serializer.Serialize(value);
        }

        public void Clear(string key)
        {
            this.SetBytes(key, null);
        }
    }
}