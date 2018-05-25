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
            {
                var list = (TestList<T>)lists[name];
                Debug.WriteLine("GetList:\t'" + name + "' = " + JsonConvert.SerializeObject(list));
                var list1 = (ISmartContractList<T>)lists[name];
                return list1;
            }

            lists[name] = new TestList<T>();
            var list2 = (ISmartContractList<T>)lists[name];
            Debug.WriteLine("GetList:\t'" + name + "' = " + JsonConvert.SerializeObject(list2));
            return list2;
        }

        public ISmartContractMapping<T> GetMapping<T>(string name)
        {
            if (mappings.ContainsKey(name))
            {
                var mapping = (TestMapping<T>)mappings[name];
                Debug.WriteLine("GetMapping:\t'" + name + "' = " + JsonConvert.SerializeObject(mapping));
                var mapping1 = (ISmartContractMapping<T>)mappings[name];
                return mapping1;
            }

            mappings[name] = new TestMapping<T>();
            var mapping2 = (ISmartContractMapping<T>)mappings[name];
            Debug.WriteLine("GetMapping:\t'" + name + "' = " + JsonConvert.SerializeObject(mapping2));
            return mapping2;
        }

        public T GetObject<T>(string key)
        {
            if (objects.ContainsKey(key))
            {
                T obj = (T)objects[key];
                string json = JsonConvert.SerializeObject(obj); // Simulate reading from storage
                Debug.WriteLine("GetObject:\t'" + key + "' = " + json);
                T newobj = JsonConvert.DeserializeObject<T>(json);
                return newobj;
            }
            else
            {
                Debug.WriteLine("GetObject:\t'" + key + "' = " + "missing");
            }

            return default(T);
        }

        public void SetObject<T>(string key, T obj)
        {
            string json = JsonConvert.SerializeObject(obj); // Simulate writing to storage
            Debug.WriteLine("SetObject:\t'" + key + "' = " + json);
            T newobj = JsonConvert.DeserializeObject<T>(json);
            objects[key] = newobj;
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
            {
                var value = mapping[key];
                string json = JsonConvert.SerializeObject(value);  // Simulate reading from storage
                Debug.WriteLine("Mapping.Get:\t['" + key + "'] = " + json);
                T newvalue = JsonConvert.DeserializeObject<T>(json);
                return newvalue;
            }

            return default(T);
        }

        public void Put(string key, T value)
        {
            if (mapping.ContainsKey(key)) Debug.WriteLine("Mapping.Put:\t['" + key + "'] = " + JsonConvert.SerializeObject(value) + " (previous value)");
            string json = JsonConvert.SerializeObject(value); // Simulate writing to storage
            Debug.WriteLine("Mapping.Put:\t['" + key + "'] = " + json);
            T newvalue = JsonConvert.DeserializeObject<T>(json);
            mapping[key] = newvalue;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.mapping.Values.GetEnumerator();
        }

        public uint Count => (uint)this.mapping.Count;
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
            var value = this.list[(int)index];
            string json = JsonConvert.SerializeObject(value); // Simulate reading from storage
            Debug.WriteLine("List.GetValue:\t(" + index.ToString() + ") = " + json);
            T newvalue = JsonConvert.DeserializeObject<T>(json);
            return newvalue;
        }

        public void SetValue(uint index, T value)
        {
            string json = JsonConvert.SerializeObject(value); // Simulate writing to storage
            Debug.WriteLine("List.SetValue:\t(" + index.ToString() + ") = " + json);
            T newvalue = JsonConvert.DeserializeObject<T>(json);
            list[(int)index] = newvalue;
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
