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

        public byte GetByte(string key)
        {
            return this.GetObject<byte>(key);
        }

        public byte[] GetByteArray(string key)
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

        public sbyte GetSbyte(string key)
        {
            return this.GetObject<sbyte>(key);
        }

        public T GetStruct<T>(string key) where T : struct
        {
            return this.GetObject<T>(key);
        }

        internal void SetObject<T>(string key, T obj)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            this.persistenceStrategy.StoreBytes(this.ContractAddress, keyBytes, this.Serializer.Serialize(obj));
        }

        public void SetByte(string key, byte value)
        {
            this.SetObject(key, value);
        }

        public void SetByteArray(string key, byte[] value)
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

        public void SetSByte(string key, sbyte value)
        {
            this.SetObject(key, value);
        }

        public void SetStruct<T>(string key, T value) where T : struct
        {
            this.SetObject(key, value);
        }

        private ISmartContractMapping<V> GetMapping<V>(string name)
        {
            return new SmartContractMapping<V>(this, name);
        }

        public ISmartContractMapping<byte> GetByteMapping(string name)
        {
            return this.GetMapping<byte>(name);
        }

        public ISmartContractMapping<byte[]> GetByteArrayMapping(string name)
        {
            return this.GetMapping<byte[]>(name);
        }

        public ISmartContractMapping<char> GetCharMapping(string name)
        {
            return this.GetMapping<char>(name);
        }

        public ISmartContractMapping<Address> GetAddressMapping(string name)
        {
            return this.GetMapping<Address>(name);
        }

        public ISmartContractMapping<bool> GetBoolMapping(string name)
        {
            return this.GetMapping<bool>(name);
        }

        public ISmartContractMapping<int> GetInt32Mapping(string name)
        {
            return this.GetMapping<int>(name);
        }

        public ISmartContractMapping<uint> GetUInt32Mapping(string name)
        {
            return this.GetMapping<uint>(name);
        }

        public ISmartContractMapping<long> GetInt64Mapping(string name)
        {
            return this.GetMapping<long>(name);
        }

        public ISmartContractMapping<ulong> GetUInt64Mapping(string name)
        {
            return this.GetMapping<ulong>(name);
        }

        public ISmartContractMapping<string> GetStringMapping(string name)
        {
            return this.GetMapping<string>(name);
        }

        public ISmartContractMapping<sbyte> GetSByteMapping(string name)
        {
            return this.GetMapping<sbyte>(name);
        }

        public ISmartContractMapping<T> GetStructMapping<T>(string name) where T : struct
        {
            return this.GetMapping<T>(name);
        }

        private ISmartContractList<T> GetList<T>(string name)
        {
            return new SmartContractList<T>(this, name);
        }

        public ISmartContractList<byte> GetByteList(string name)
        {
            return this.GetList<byte>(name);
        }

        public ISmartContractList<byte[]> GetByteArrayList(string name)
        {
            return this.GetList<byte[]>(name);
        }

        public ISmartContractList<char> GetCharList(string name)
        {
            return this.GetList<char>(name);
        }

        public ISmartContractList<Address> GetAddressList(string name)
        {
            return this.GetList<Address>(name);
        }

        public ISmartContractList<bool> GetBoolList(string name)
        {
            return this.GetList<bool>(name);
        }

        public ISmartContractList<int> GetInt32List(string name)
        {
            return this.GetList<int>(name);
        }

        public ISmartContractList<uint> GetUInt32List(string name)
        {
            return this.GetList<uint>(name);
        }

        public ISmartContractList<long> GetInt64List(string name)
        {
            return this.GetList<long>(name);
        }

        public ISmartContractList<ulong> GetUInt64List(string name)
        {
            return this.GetList<ulong>(name);
        }

        public ISmartContractList<string> GetStringList(string name)
        {
            return this.GetList<string>(name);
        }

        public ISmartContractList<sbyte> GetSByteList(string name)
        {
            return this.GetList<sbyte>(name);
        }

        public ISmartContractList<T> GetStructList<T>(string name) where T : struct
        {
            return this.GetList<T>(name);
        }
    }
}