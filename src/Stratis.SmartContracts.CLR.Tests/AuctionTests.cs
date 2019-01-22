using System;
using System.Collections.Generic;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class AuctionTests
    {
        private readonly Address TestAddress;
        private TestSmartContractState smartContractState;
        private const ulong Balance = 0;
        private const ulong GasLimit = 10000;
        private const ulong Value = 0;

        public AuctionTests()
        {
            var network = new SmartContractsRegTest();

            this.TestAddress = "0x0000000000000000000000000000000000000001".HexToAddress();

            var block = new TestBlock
            {
                Coinbase = this.TestAddress,
                Number = 1
            };
            var message = new TestMessage
            {
                ContractAddress = this.TestAddress,
                Sender = this.TestAddress,
                Value = Value
            };
            var getBalance = new Func<ulong>(() => Balance);
            var persistentState = new TestPersistentState();
            var serializer = new Serializer(new ContractPrimitiveSerializer(network));
            this.smartContractState = new TestSmartContractState(
                block,
                message,
                persistentState,
                serializer,
                null,
                null,
                getBalance,
                null,
                null
            );
        }

        [Fact]
        public void TestCreation()
        {
            const ulong duration = 20;
            var contract = new Auction(this.smartContractState, duration);
            Assert.Equal(this.TestAddress, this.smartContractState.PersistentState.GetAddress("Owner"));
            Assert.False(this.smartContractState.PersistentState.GetBool("HasEnded"));
            Assert.Equal(duration + this.smartContractState.Block.Number, this.smartContractState.PersistentState.GetUInt64("EndBlock"));
        }

        [Fact]
        public void TestBidding()
        {
            const ulong duration = 20;
            var contract = new Auction(this.smartContractState, duration);

            ((TestMessage)this.smartContractState.Message).Value = 100;
            Assert.Equal(default(Address), this.smartContractState.PersistentState.GetAddress("HighestBidder"));
            Assert.Equal(0uL, this.smartContractState.PersistentState.GetUInt64("HighestBid"));

            contract.Bid();
            Assert.Equal(this.TestAddress, this.smartContractState.PersistentState.GetAddress("HighestBidder"));
            Assert.Equal(100uL, this.smartContractState.PersistentState.GetUInt64("HighestBid"));

            ((TestMessage)this.smartContractState.Message).Value = 90;
            Assert.ThrowsAny<Exception>(() => contract.Bid());
        }
    }

    public class TestSmartContractState : ISmartContractState
    {
        public TestSmartContractState(
            IBlock block,
            IMessage message,
            IPersistentState persistentState,
            ISerializer serializer,
            IGasMeter gasMeter,
            IInternalTransactionExecutor transactionExecutor,
            Func<ulong> getBalance,
            IInternalHashHelper hashHelper,
            IContractLogger contractLogger)
        {
            this.Block = block;
            this.Message = message;
            this.PersistentState = persistentState;
            this.Serializer = serializer;
            this.GasMeter = gasMeter;
            this.InternalTransactionExecutor = transactionExecutor;
            this.GetBalance = getBalance;
            this.InternalHashHelper = hashHelper;
            this.ContractLogger = contractLogger;
        }

        public IBlock Block { get; }
        public IMessage Message { get; }
        public IPersistentState PersistentState { get; }
        public ISerializer Serializer { get; }
        public IGasMeter GasMeter { get; }
        public IInternalTransactionExecutor InternalTransactionExecutor { get; }
        public Func<ulong> GetBalance { get; }
        public IInternalHashHelper InternalHashHelper { get; }
        public IContractLogger ContractLogger { get; }
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

        public ulong Value { get; set; }
    }

    public class TestPersistentState : IPersistentState
    {
        private Dictionary<string, object> objects = new Dictionary<string, object>();

        private T GetObject<T>(string key)
        {
            if (this.objects.ContainsKey(key))
                return (T)this.objects[key];

            return default(T);
        }

        public bool IsContract(Address address)
        {
            throw new NotImplementedException();
        }

        public byte[] GetBytes(byte[] key)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public void SetBytes(byte[] key, byte[] value)
        {
            throw new NotImplementedException();
        }

        private void SetObject<T>(string key, T obj)
        {
            this.objects[key] = obj;
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
            throw new NotImplementedException();
        }

        public void Clear(string key)
        {
            throw new NotImplementedException();
        }
    }
}
