using System;
using System.Collections.Generic;
using Stratis.SmartContracts;

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

        public ISmartContractList<T> GetList<T>(string name)
        {
            throw new NotImplementedException();
        }

        public ISmartContractMapping<T> GetMapping<T>(string name)
        {
            if (mappings.ContainsKey(name))
                return (ISmartContractMapping<T>)mappings[name];

            mappings[name] = new TestMapping<T>();
            return (ISmartContractMapping<T>)mappings[name];
        }

        public T GetObject<T>(string key)
        {
            if (objects.ContainsKey(key))
                return (T)objects[key];

            return default(T);
        }

        public void SetObject<T>(string key, T obj)
        {
            objects[key] = obj;
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
}
