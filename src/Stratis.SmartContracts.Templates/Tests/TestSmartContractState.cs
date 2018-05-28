using System;
using System.Collections.Generic;
using Stratis.SmartContracts;
using System.Text;

namespace $safeprojectname$
{
    public class TestSmartContractState : ISmartContractState
    {
        public TestSmartContractState(
            IBlock block,
            IMessage message,
            IPersistentState persistentState,
            IGasMeter gasMeter,
            IInternalTransactionExecutor transactionExecutor,
            Func<ulong> getBalance,
            IInternalHashHelper hashHelper)
        {
            this.Block = block;
            this.Message = message;
            this.PersistentState = persistentState;
            this.GasMeter = gasMeter;
            this.InternalTransactionExecutor = transactionExecutor;
            this.GetBalance = getBalance;
            this.InternalHashHelper = hashHelper;
        }

        public IBlock Block { get; set; }
        public IMessage Message { get; }
        public IPersistentState PersistentState { get; }
        public IGasMeter GasMeter { get; }
        public IInternalTransactionExecutor InternalTransactionExecutor { get; set; }
        public Func<ulong> GetBalance { get; }
        public IInternalHashHelper InternalHashHelper { get; }
    }

    public class TestInternalTransactionExecutor : IInternalTransactionExecutor
    {
        public Dictionary<Address, ulong> ChainBalanceTracker;
        public Address ContractAddress;

        public TestInternalTransactionExecutor(Dictionary<Address, ulong> chainBalanceTracker, Address contractAddress)
        {
            this.ChainBalanceTracker = chainBalanceTracker;
            this.ContractAddress = contractAddress;
        }
        public ITransferResult TransferFunds(ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer, TransferFundsToContract transferFundsToContractDetails)
        {
            ChainBalanceTracker[ContractAddress] = ChainBalanceTracker[ContractAddress] - amountToTransfer;
            ChainBalanceTracker[addressTo] = ChainBalanceTracker[addressTo] + amountToTransfer;
            var fakeTransferResult = new TestTransferResult
            {
                Success = true
            };
            return fakeTransferResult;
        }
    }

    public class TestTransferResult : ITransferResult
    {
        public object ReturnValue { get; }
        public Exception ThrownException { get; }
        public bool Success { get; set; }

        public ulong Number { get; set; }
    }

    public class TestBlock : IBlock
    {
        public Address Coinbase { get; set; }

        public ulong Number { get; set; }
    }

    public class TestMessage : IMessage
    {
        public Address ContractAddress { get; set; }

        public Address Sender { get; set; }

        public Gas GasLimit { get; set; }

        public ulong Value { get; set; }
    }

    public class TestPersistentState : IPersistentState
    {
        private Dictionary<string, object> objects = new Dictionary<string, object>();
        private Dictionary<string, TestMapping> mappings = new Dictionary<string, TestMapping>();
        private Dictionary<string, TestList> lists = new Dictionary<string, TestList>();
    
        public ISmartContractList<T> GetList<T>(string name)
        {
            if (lists.ContainsKey(name))
                return (ISmartContractList<T>)lists[name];

            lists[name] = new TestList<T>();
            return (ISmartContractList<T>)lists[name];
        }

        public ISmartContractMapping<T> GetMapping<T>(string name)
        {
            if (mappings.ContainsKey(name))
                return (ISmartContractMapping<T>)mappings[name];

            mappings[name] = new TestMapping<T>();
            return (ISmartContractMapping<T>)mappings[name];
        }

        private T GetObject<T>(string key)
        {
            if (objects.ContainsKey(key))
                return (T)objects[key];

            return default(T);
        }

        private void SetObject<T>(string key, T obj)
        {
            objects[key] = obj;
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

        public ISmartContractMapping<T> GetStructMapping<T>(string name) where T : struct
        {
            return this.GetMapping<T>(name);
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
    }

    public abstract class TestMapping
    {

    }

    public class TestMapping<T> : TestMapping, ISmartContractMapping<T>
    {
        private readonly Dictionary<string, T> mapping;

        public TestMapping()
        {
            this.mapping = new Dictionary<string, T>();
        }

        public T this[string key]
        {
            get => Get(key);
            set => Put(key, value);
        }

        public T Get(string key)
        {
            if (mapping.ContainsKey(key))
                return mapping[key];

            return default(T);
        }

        public void Put(string key, T value)
        {
            mapping[key] = value;
        }
    }

    public abstract class TestList
    {

    }

    public class TestList<T> : TestList, ISmartContractList<T>
    {
        private readonly List<T> list;

        public TestList()
        {
            this.list = new List<T>();
        }
        public void Add(T item)
        {
            this.list.Add(item);
        }

        public T GetValue(uint index)
        {
            return this.list[(int)index];
        }

        public void SetValue(uint index, T value)
        {
            list[(int)index] = value;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.list.GetEnumerator();
        }

        public uint Count => (uint)this.list.Count;

        public T this[uint key] { get => GetValue(key); set => SetValue(key, value); }
    }

    public class TestGasMeter : IGasMeter
    {
        public Gas GasAvailable { get; private set; }

        public Gas GasConsumed => (Gas)(this.GasLimit - this.GasAvailable);

        public Gas GasLimit { get; }

        public TestGasMeter(Gas gasAvailable)
        {
            this.GasAvailable = gasAvailable;
            this.GasLimit = gasAvailable;
        }

        public void Spend(Gas gasToSpend)
        {
            if (this.GasAvailable >= gasToSpend)
            {
                this.GasAvailable -= gasToSpend;
                return;
            }

            this.GasAvailable = (Gas)0;

            throw new Exception("Went over gas limit of " + this.GasLimit);
        }
    }

    public class TestInternalHashHelper : IInternalHashHelper
    {
        public byte[] Keccak256(byte[] toHash)
        {
            return Encoding.ASCII.GetBytes("DO NOT USE");
        }
    }
}
