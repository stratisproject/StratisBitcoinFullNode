using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.SmartContracts;

namespace $safeprojectname$
{
    [TestClass]
    public class SmartContractTests
    {
        private TestSmartContractState smartContractState;
        private const ulong Balance = 10000;

        [TestInitialize]
        public void Initialize()
        {
            var block = new Block(0, new Address());
            var message = new Message(new Address(), new Address(), 0, (Gas)0);
            var getBalance = new Func<ulong>(() => Balance);

            this.smartContractState = new TestSmartContractState(
                block,
                message,
                null,
                null,
                null,
                getBalance,
                null
            );
        }

        [TestMethod]
        public void TestMethod()
        {
            var contract = new Contract(smartContractState);

            Assert.AreEqual("Test", contract.Test());
        }

        [TestMethod]
        public void TestBalance()
        {
            var contract = new Contract(smartContractState);

            Assert.AreEqual(Balance, contract.Balance);
        }
    }

    public class TestSmartContractState : ISmartContractState
    {
        public TestSmartContractState(
            Block block,
            Message message,
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

        public Block Block { get; }
        public Message Message { get; }
        public IPersistentState PersistentState { get; }
        public IGasMeter GasMeter { get; }
        public IInternalTransactionExecutor InternalTransactionExecutor { get; }
        public Func<ulong> GetBalance { get; }
        public IInternalHashHelper InternalHashHelper { get; }
    }
}
