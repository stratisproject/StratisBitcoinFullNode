using System;
using System.Collections.Generic;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class AuctionTests
    {
        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";
        private TestSmartContractState smartContractState;
        private const ulong Balance = 0;
        private const ulong GasLimit = 10000;
        private const ulong Value = 0;

        public AuctionTests()
        {
            var block = new TestBlock
            {
                Coinbase = TestAddress,
                Number = 1
            };
            var message = new TestMessage
            {
                ContractAddress = TestAddress,
                GasLimit = (Gas)GasLimit,
                Sender = TestAddress,
                Value = Value
            };
            var getBalance = new Func<ulong>(() => Balance);
            var persistentState = new TestPersistentState();
            var network = new SmartContractsRegTest();
            var serializer = new ContractPrimitiveSerializer(network);
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
            var contract = new Auction(smartContractState, duration);
            Assert.Equal(TestAddress, smartContractState.PersistentState.GetAsAddress("Owner"));
            Assert.False(smartContractState.PersistentState.GetAsBool("HasEnded"));
            Assert.Equal(duration + smartContractState.Block.Number, smartContractState.PersistentState.GetAsUInt64("EndBlock"));
        }

        [Fact]
        public void TestBidding()
        {
            const ulong duration = 20;
            var contract = new Auction(this.smartContractState, duration);

            ((TestMessage)smartContractState.Message).Value = 100;
            Assert.Null(smartContractState.PersistentState.GetAsAddress("HighestBidder").Value);
            Assert.Equal(0uL, smartContractState.PersistentState.GetAsUInt64("HighestBid"));

            contract.Bid();
            Assert.NotNull(smartContractState.PersistentState.GetAsAddress("HighestBidder").Value);
            Assert.Equal(100uL, smartContractState.PersistentState.GetAsUInt64("HighestBid"));

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

        public Gas GasLimit { get; set; }

        public ulong Value { get; set; }
    }

    public class TestPersistentState : IPersistentState
    {
        private Dictionary<string, object> objects = new Dictionary<string, object>();

        public byte[] GetBytes(string key)
        {
            throw new NotImplementedException();
        }

        public void SetBytes(string key, byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public char GetAsChar(string key)
        {
            throw new NotImplementedException();
        }

        public Address GetAsAddress(string key)
        {
            throw new NotImplementedException();
        }

        public bool GetAsBool(string key)
        {
            throw new NotImplementedException();
        }

        public int GetAsInt32(string key)
        {
            throw new NotImplementedException();
        }

        public uint GetAsUInt32(string key)
        {
            throw new NotImplementedException();
        }

        public long GetAsInt64(string key)
        {
            throw new NotImplementedException();
        }

        public ulong GetAsUInt64(string key)
        {
            throw new NotImplementedException();
        }

        public string GetAsString(string key)
        {
            throw new NotImplementedException();
        }

        public T GetAsStruct<T>(string key) where T : struct
        {
            throw new NotImplementedException();
        }

        public void SetChar(string key, char value)
        {
            throw new NotImplementedException();
        }

        public void SetAddress(string key, Address value)
        {
            throw new NotImplementedException();
        }

        public void SetBool(string key, bool value)
        {
            throw new NotImplementedException();
        }

        public void SetInt32(string key, int value)
        {
            throw new NotImplementedException();
        }

        public void SetUInt32(string key, uint value)
        {
            throw new NotImplementedException();
        }

        public void SetInt64(string key, long value)
        {
            throw new NotImplementedException();
        }

        public void SetUInt64(string key, ulong value)
        {
            throw new NotImplementedException();
        }

        public void SetString(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void SetStruct<T>(string key, T value) where T : struct
        {
            throw new NotImplementedException();
        }
    }
}
