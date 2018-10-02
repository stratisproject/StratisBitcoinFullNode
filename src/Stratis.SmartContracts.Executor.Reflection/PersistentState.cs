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

        // Will always return byte[0]
        public byte[] GetBytes(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            return this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
        }

        public void SetBytes(string key, byte[] bytes)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            this.persistenceStrategy.StoreBytes(this.ContractAddress, keyBytes, bytes);
        }

        public char GetAsChar(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToChar(toReturn);
        }

        public Address GetAsAddress(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToAddress(toReturn);
        }

        public bool GetAsBool(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToBool(toReturn);
        }

        public int GetAsInt32(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToInt32(toReturn);
        }

        public uint GetAsUInt32(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToUInt32(toReturn);
        }

        public long GetAsInt64(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToInt64(toReturn);
        }

        public ulong GetAsUInt64(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToUInt64(toReturn);
        }

        public string GetAsString(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToString(toReturn);
        }

        public T GetAsStruct<T>(string key) where T : struct
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToStruct<T>(toReturn);
        }

        public T[] GetAsArray<T>(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] toReturn = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);
            return Serializer.ToArray<T>(toReturn);
        }

        public void SetChar(string key, char value)
        {
            byte[] valBytes = Serializer.Serialize(value);
            this.SetBytes(key, valBytes);
        }

        public void SetAddress(string key, Address value)
        {
            byte[] valBytes = Serializer.Serialize(value);
            this.SetBytes(key, valBytes);
        }

        public void SetBool(string key, bool value)
        {
            byte[] valBytes = Serializer.Serialize(value);
            this.SetBytes(key, valBytes);
        }

        public void SetInt32(string key, int value)
        {
            byte[] valBytes = Serializer.Serialize(value);
            this.SetBytes(key, valBytes);
        }

        public void SetUInt32(string key, uint value)
        {
            byte[] valBytes = Serializer.Serialize(value);
            this.SetBytes(key, valBytes);
        }

        public void SetInt64(string key, long value)
        {
            byte[] valBytes = Serializer.Serialize(value);
            this.SetBytes(key, valBytes);
        }

        public void SetUInt64(string key, ulong value)
        {
            byte[] valBytes = Serializer.Serialize(value);
            this.SetBytes(key, valBytes);
        }

        public void SetString(string key, string value)
        {
            byte[] valBytes = Serializer.Serialize(value);
            this.SetBytes(key, valBytes);
        }

        public void SetStruct<T>(string key, T value) where T : struct
        {
            byte[] valBytes = Serializer.SerializeStruct(value);
            this.SetBytes(key, valBytes);
        }
    }
}