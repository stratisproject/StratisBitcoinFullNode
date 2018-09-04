using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Patricia;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SmartContractListTests
    {
        private readonly Network network;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;

        public SmartContractListTests()
        {
            this.network = new SmartContractsRegTest();
            this.contractPrimitiveSerializer = new ContractPrimitiveSerializer(this.network);
        }

        [Fact]
        public void SmartContractList_ListHasCountTest()
        {
            var listName = "testList";

            var source = new MemoryDictionarySource();
            var root = new ContractStateRoot(source);

            IContractState state = root.StartTracking();
            IPersistenceStrategy persistenceStrategy = new PersistenceStrategy(state);

            var persistentState = new PersistentState(
                persistenceStrategy,
                this.contractPrimitiveSerializer,
                uint160.One);

            var list = new SmartContractList<string>(persistentState, listName);

            Assert.Equal((uint)0, list.Count);
            list.Add("Test");
            Assert.Equal((uint)1, list.Count);
        }

        [Fact]
        public void SmartContractList_ListHasValueTest()
        {
            var listName = "testList";

            var source = new MemoryDictionarySource();
            var root = new ContractStateRoot(source);

            IContractState state = root.StartTracking();
            IPersistenceStrategy persistenceStrategy = new PersistenceStrategy(state);

            var persistentState = new PersistentState(
                persistenceStrategy,
                this.contractPrimitiveSerializer,
                uint160.One);

            var list = new SmartContractList<string>(persistentState, listName);

            Assert.Equal((uint)0, list.Count);

            var testItem = "Test";
            list.Add(testItem);
            var item1 = list.GetValue(0);
            Assert.Equal(testItem, item1);
        }

        [Fact]
        public void SmartContractList_ListHasMultipleValuesTest()
        {
            var listName = "testList";

            var source = new MemoryDictionarySource();
            var root = new ContractStateRoot(source);

            IContractState state = root.StartTracking();
            IPersistenceStrategy persistenceStrategy = new PersistenceStrategy(state);

            var persistentState = new PersistentState(
                persistenceStrategy,
                this.contractPrimitiveSerializer,
                uint160.One);

            var list = new SmartContractList<string>(persistentState, listName);

            Assert.Equal((uint)0, list.Count);

            var testItem = "Test";
            var testItem2 = "Test2";
            var testItem3 = "Test3";

            // Set a value in the list
            list.Add(testItem);
            list.Add(testItem2);
            list.Add(testItem3);

            Assert.Equal((uint)3, list.Count);

            var item1 = list.GetValue(0);
            var item2 = list.GetValue(1);
            var item3 = list.GetValue(2);

            Assert.Equal(testItem, item1);
            Assert.Equal(testItem2, item2);
            Assert.Equal(testItem3, item3);
        }

        [Fact]
        public void SmartContractList_CanAccessValueUsingIndexTest()
        {
            var listName = "testList";

            var source = new MemoryDictionarySource();
            var root = new ContractStateRoot(source);

            IContractState state = root.StartTracking();
            IPersistenceStrategy persistenceStrategy = new PersistenceStrategy(state);
            var persistentState = new PersistentState(
                persistenceStrategy,
                this.contractPrimitiveSerializer,
                uint160.One);

            var list = new SmartContractList<string>(persistentState, listName);

            Assert.Equal((uint)0, list.Count);

            // Set a value in the list
            list.Add("Test");
            list.Add("Test2");

            var testItem = list.GetValue(0);
            var testItem2 = list.GetValue(1);

            var firstItemHash = $"{listName}[0]";
            var secondItemHash = $"{listName}[1]";

            var item = persistentState.GetString(firstItemHash);
            var item2 = persistentState.GetString(secondItemHash);

            Assert.Equal(testItem, item);
            Assert.Equal(testItem2, item2);
        }

        [Fact]
        public void SmartContractList_ListEnumerationTest()
        {
            var listName = "testList";

            var source = new MemoryDictionarySource();
            var root = new ContractStateRoot(source);

            IContractState state = root.StartTracking();
            IPersistenceStrategy persistenceStrategy = new PersistenceStrategy(state);
            var persistentState = new PersistentState(
                persistenceStrategy,
                this.contractPrimitiveSerializer,
                uint160.One);

            var list = new SmartContractList<string>(persistentState, listName);

            Assert.Equal((uint)0, list.Count);

            var items = new List<string>
            {
                "this is a test",
                "we should try to find a way",
                "to paramaterize our tests"
            };

            foreach (var item in items)
            {
                // No AddRange
                list.Add(item);
            }

            Assert.Equal((uint)items.Count, list.Count);

            for (var i = 0; i < list.Count; i++)
            {
                Assert.Equal(items[i], list.GetValue((uint)i));
            }
        }
    }

    public class PersistenceStrategy : IPersistenceStrategy
    {
        private readonly IContractState stateDb;

        public PersistenceStrategy(IContractState stateDb)
        {
            this.stateDb = stateDb;
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            return this.stateDb.GetStorageValue(address, key);
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value)
        {
            this.stateDb.SetStorageValue(address, key, value);
        }
    }
}