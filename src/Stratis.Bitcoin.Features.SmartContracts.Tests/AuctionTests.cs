using System;
using System.Collections.Generic;
using Stratis.SmartContracts;
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
            this.smartContractState = new TestSmartContractState(
                block,
                message,
                persistentState,
                null,
                null,
                getBalance,
                null
            );
        }

        [Fact]
        public void TestCreation()
        {
            const ulong duration = 20;
            var contract = new Auction(this.smartContractState, duration);
            Assert.Equal(TestAddress, this.smartContractState.PersistentState.GetObject<Address>("Owner"));
            Assert.False(this.smartContractState.PersistentState.GetObject<bool>("HasEnded"));
            Assert.Equal(duration + this.smartContractState.Block.Number, this.smartContractState.PersistentState.GetObject<ulong>("EndBlock"));
        }

        [Fact]
        public void TestBidding()
        {
            const ulong duration = 20;
            var contract = new Auction(this.smartContractState, duration);

            ((TestMessage)this.smartContractState.Message).Value = 100;
            Assert.Null(this.smartContractState.PersistentState.GetObject<Address>("HighestBidder").Value);
            Assert.Equal(0uL, this.smartContractState.PersistentState.GetObject<ulong>("HighestBid"));

            contract.Bid();
            Assert.NotNull(this.smartContractState.PersistentState.GetObject<Address>("HighestBidder").Value);
            Assert.Equal(100uL, this.smartContractState.PersistentState.GetObject<ulong>("HighestBid"));

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

        public IBlock Block { get; }
        public IMessage Message { get; }
        public IPersistentState PersistentState { get; }
        public IGasMeter GasMeter { get; }
        public IInternalTransactionExecutor InternalTransactionExecutor { get; }
        public Func<ulong> GetBalance { get; }
        public IInternalHashHelper InternalHashHelper { get; }
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

        public ISmartContractList<T> GetList<T>(string name)
        {
            throw new NotImplementedException();
        }

        public ISmartContractMapping<V> GetMapping<V>(string name)
        {
            throw new NotImplementedException();
        }

        public T GetObject<T>(string key)
        {
            if (this.objects.ContainsKey(key))
                return (T)this.objects[key];

            return default(T);
        }

        public void SetObject<T>(string key, T obj)
        {
            this.objects[key] = obj;
        }
    }
}
