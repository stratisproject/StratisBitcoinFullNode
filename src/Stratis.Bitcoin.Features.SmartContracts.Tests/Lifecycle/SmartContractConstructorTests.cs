using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Lifecycle;
using Xunit;
using InternalHashHelper = Stratis.SmartContracts.Core.Hashing.InternalHashHelper;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Lifecycle
{
    public class SmartContractConstructorTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ISmartContractState state;

        public SmartContractConstructorTests()
        {
            this.loggerFactory = new Mock<ILoggerFactory>().Object;
            this.state = new SmartContractState(
                new Block(0, new Address()),
                new Message(new Address(), new Address(), 0, (Gas)0),
                new PersistentState(null, 0, null),
                new GasMeter((Gas)1000),
                new InternalTransactionExecutor(null, null, null, null, new BasicKeyEncodingStrategy(), this.loggerFactory, network: null),
                new InternalHashHelper(),
                () => 10000);
        }

        [Fact]
        public void SmartContract_LifecycleResult_ExceptionSuccessFalse()
        {
            var e = new Exception();
            var constructionResult = new LifecycleResult(e);

            Assert.False(constructionResult.Success);
            Assert.Equal(e, constructionResult.Exception);
            Assert.Null(constructionResult.Object);
        }

        [Fact]
        public void SmartContract_LifecycleResult_ObjectSuccessTrue()
        {
            var contract = new NoParamContract(this.state);
            var constructionResult = new LifecycleResult(contract);

            Assert.True(constructionResult.Success);
            Assert.Equal(contract, constructionResult.Object);
            Assert.Null(constructionResult.Exception);
        }

        [Fact]
        public void SmartContract_Constructor_NonpublicStateFieldsSetSuccess()
        {
            LifecycleResult constructionResult = SmartContractConstructor
                .Construct(typeof(NoParamContract), this.state);

            Assert.True(constructionResult.Success);

            SmartContract contract = constructionResult.Object;

            FieldInfo[] fields = typeof(SmartContract).GetFields(
                BindingFlags.Instance
                | BindingFlags.NonPublic
                );

            foreach (FieldInfo field in fields)
            {
                object value = field.GetValue(contract);

                switch (field.Name)
                {
                    case "Block":
                        Assert.Equal(this.state.Block, value);
                        break;
                    case "Message":
                        Assert.Equal(this.state.Message, value);
                        break;
                    case "PersistentState":
                        Assert.Equal(this.state.PersistentState, value);
                        break;
                    case "gasMeter":
                        Assert.Equal(this.state.GasMeter, value);
                        break;
                    case "getBalance":
                        Assert.Equal(this.state.GetBalance, value);
                        break;
                    case "internalTransactionExecutor":
                        Assert.Equal(this.state.InternalTransactionExecutor, value);
                        break;
                    case "internalHashHelper":
                        Assert.Equal(this.state.InternalHashHelper, value);
                        break;
                    case "smartContractState":
                        Assert.Equal(this.state, value);
                        break;
                }
            }
        }

        [Fact]
        public void SmartContract_Constructor_NoParamSuccess()
        {
            LifecycleResult constructionResult = SmartContractConstructor
                .Construct(typeof(NoParamContract), this.state);

            Assert.True(constructionResult.Success);
            Assert.NotNull(constructionResult.Object);
        }

        [Fact]
        public void SmartContract_Constructor_OneParamSuccess()
        {
            ulong startBlock = 12345;

            LifecycleResult constructionResult = SmartContractConstructor
                .Construct(typeof(OneParamContract), this.state, startBlock);

            Assert.True(constructionResult.Success);
            Assert.NotNull(constructionResult.Object);
        }

        [Fact]
        public void SmartContract_Constructor_ConstructorInvokedSuccess()
        {
            LifecycleResult constructionResult = SmartContractConstructor
                .Construct(typeof(ConstructorInvokedContract), this.state);

            Assert.True(constructionResult.Success);
            Assert.NotNull(constructionResult.Object);
            Assert.True(((ConstructorInvokedContract)constructionResult.Object).ConstructorInvoked);
        }

        [Fact]
        public void SmartContract_Constructor_InvalidParamSuccess()
        {
            ulong startBlock = 12345;

            LifecycleResult constructionResult = SmartContractConstructor
                .Construct(typeof(NoParamContract), this.state, startBlock);

            Assert.False(constructionResult.Success);
            Assert.Null(constructionResult.Object);
            Assert.NotNull(constructionResult.Exception);
        }
    }

    public class NoParamContract : SmartContract
    {
        public NoParamContract(ISmartContractState smartContractState)
            : base(smartContractState)
        {
        }
    }

    public class OneParamContract : SmartContract
    {
        public OneParamContract(ISmartContractState smartContractState, ulong startBlock)
            : base(smartContractState)
        {
            this.StartBlock = startBlock;
        }

        public ulong StartBlock { get; }
    }

    public class ConstructorInvokedContract : SmartContract
    {
        public ConstructorInvokedContract(ISmartContractState smartContractState)
            : base(smartContractState)
        {
            this.ConstructorInvoked = true;
        }


        public bool ConstructorInvoked { get; }
    }
}