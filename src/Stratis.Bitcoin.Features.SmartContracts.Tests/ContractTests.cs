using System;
using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ContractTests
    {
        private ISmartContractState state;
        private TestContract instance;
        private Type type;
        private uint160 address;
        private IContract contract;

        public class HasFallback : SmartContract
        {
            public HasFallback(ISmartContractState smartContractState)
                : base(smartContractState)
            {
            }

            public override void Fallback()
            {
                this.FallbackInvoked = true;
            }

            public bool FallbackInvoked { get; private set; }
        }

        public class HasNoFallback : SmartContract
        {
            public HasNoFallback(ISmartContractState smartContractState)
                : base(smartContractState)
            {
            }
        }

        public class TestContract : SmartContract
        {
            public TestContract(ISmartContractState smartContractState) 
                : base(smartContractState)
            {
                this.ConstructorCalledCount++;
                this.State = smartContractState;
            }

            public TestContract(ISmartContractState smartContractState, int param)
                : base(smartContractState)
            {
                this.ConstructorCalledCount++;
                this.State = smartContractState;
                this.Param = param;
            }

            public TestContract(int a, string b)
            : base(null)
            {

            }

            public void Test1()
            {
                this.Test1Called = true;
            }

            public int Test2(int param)
            {
                return param;
            }

            private void TestPrivate()
            {
            }

            public void TestException()
            {
                throw new Exception("Something went wrong");
            }

            public void TestOutOfGas()
            {
                throw new OutOfGasException("Out of gas");
            }

            public bool Test1Called { get; set; }

            public int Param { get; set; }

            public ISmartContractState State { get; }

            public int ConstructorCalledCount { get; }
        }

        public ContractTests()
        {
            var gasMeter = new Mock<IGasMeter>();
            var internalTxExecutor = new Mock<IInternalTransactionExecutor>();
            var internalHashHelper = new Mock<IInternalHashHelper>();
            var persistentState = new Mock<IPersistentState>();
            var block = new Mock<IBlock>();
            var message = new Mock<IMessage>();
            Func<ulong> getBalance = () => 1;

            this.state = Mock.Of<ISmartContractState>(
                g => g.GasMeter == gasMeter.Object
                     && g.InternalTransactionExecutor == internalTxExecutor.Object
                     && g.InternalHashHelper == internalHashHelper.Object
                     && g.PersistentState == persistentState.Object
                     && g.Block == block.Object
                     && g.Message == message.Object
                     && g.GetBalance == getBalance);
            this.type = typeof(TestContract);
            this.address = uint160.One;
            this.contract = Contract.CreateUninitialized(this.type, this.state, this.address);
            this.instance = (TestContract) this.contract.GetPrivateFieldValue("instance");
        }

        [Fact]
        public void Invoke_Constructor_With_Null_Params()
        {
            IContractInvocationResult result = this.contract.InvokeConstructor(null);

            Assert.Equal(ContractInvocationErrorType.None, result.InvocationErrorType);
            Assert.True(result.IsSuccess);
            // We expect the count to be 2 because we call the constructor when setting up the test as well
            Assert.Equal(1, this.instance.ConstructorCalledCount);
            Assert.Equal(this.state, this.instance.State);
        }

        [Fact]
        public void Invoke_Constructor_With_One_Params()
        {
            var param = 99999;
            IContractInvocationResult result = this.contract.InvokeConstructor(new List<object> { param });

            Assert.Equal(ContractInvocationErrorType.None, result.InvocationErrorType);
            Assert.True(result.IsSuccess);
            Assert.Equal(1, this.instance.ConstructorCalledCount);
            Assert.Equal(param, this.instance.Param);
            Assert.Equal(this.state, this.instance.State);
        }

        [Fact]
        public void Invoke_Constructor_With_Too_Many_Params_First_Correct()
        {
            var param = 99999;
            IContractInvocationResult result = this.contract.InvokeConstructor(
                new List<object>
                {
                    param, "abc"
                });

            Assert.Equal(ContractInvocationErrorType.MethodDoesNotExist, result.InvocationErrorType);
            Assert.False(result.IsSuccess);
            Assert.Equal(0, this.instance.ConstructorCalledCount);
        }

        [Fact]
        public void Invoke_Constructor_With_Too_Many_Params_None_Correct()
        {
            IContractInvocationResult result = this.contract.InvokeConstructor(
                new List<object>
                {
                    "123", "abc"
                });

            Assert.Equal(ContractInvocationErrorType.MethodDoesNotExist, result.InvocationErrorType);
            Assert.False(result.IsSuccess);
            Assert.Equal(0, this.instance.ConstructorCalledCount);
        }

        [Fact]
        public void Invoke_Method_With_Null_Params()
        {
            var methodCall = new MethodCall("Test1");
            IContractInvocationResult result = this.contract.Invoke(methodCall);

            Assert.True(result.IsSuccess);
            Assert.True(this.instance.Test1Called);
            Assert.Equal(0, this.instance.ConstructorCalledCount);
        }

        [Fact]
        public void Invoke_Method_With_One_Params()
        {
            var param = 9999;
            var methodCall = new MethodCall("Test2", new object[] { param });

            IContractInvocationResult result = this.contract.Invoke(methodCall);

            Assert.True(result.IsSuccess);
            Assert.Equal(param, result.Return);
        }

        [Fact]
        public void Invoke_Private_Method_Fails()
        {
            var methodCall = new MethodCall("TestPrivate");

            IContractInvocationResult result = this.contract.Invoke(methodCall);

            Assert.False(result.IsSuccess);
            Assert.Equal(ContractInvocationErrorType.MethodDoesNotExist, result.InvocationErrorType);
        }

        [Fact]
        public void Invoke_Method_Throws_Exception()
        {
            var methodCall = new MethodCall("TestException");

            IContractInvocationResult result = this.contract.Invoke(methodCall);

            Assert.False(result.IsSuccess);
            Assert.Equal(ContractInvocationErrorType.MethodThrewException, result.InvocationErrorType);
        }

        [Fact]
        public void Invoke_Method_Throws_OutOfGasException()
        {
            var methodCall = new MethodCall("TestOutOfGas");

            IContractInvocationResult result = this.contract.Invoke(methodCall);

            Assert.False(result.IsSuccess);
            Assert.Equal(ContractInvocationErrorType.OutOfGas, result.InvocationErrorType);
        }

        [Fact]
        public void Invoke_Method_Sets_State()
        {
            var methodCall = new MethodCall("TestOutOfGas");

            this.contract.Invoke(methodCall);

            var gasMeter = this.instance.GetBaseTypePrivateFieldValue("gasMeter");
            var block = this.instance.GetBaseTypePrivateFieldValue("Block");
            var getBalance = this.instance.GetBaseTypePrivateFieldValue("getBalance");
            var internalTransactionExecutor = this.instance.GetBaseTypePrivateFieldValue("internalTransactionExecutor");
            var internalHashHelper = this.instance.GetBaseTypePrivateFieldValue("internalHashHelper");
            var message = this.instance.GetBaseTypePrivateFieldValue("Message");
            var persistentState = this.instance.GetBaseTypePrivateFieldValue("PersistentState");
            var smartContractState = this.instance.GetBaseTypePrivateFieldValue("smartContractState");

            Assert.NotNull(gasMeter);
            Assert.Equal(this.state.GasMeter, gasMeter);
            Assert.NotNull(block);
            Assert.Equal(this.state.Block, block);
            Assert.NotNull(getBalance);
            Assert.Equal(this.state.GetBalance, getBalance);
            Assert.NotNull(internalTransactionExecutor);
            Assert.Equal(this.state.InternalTransactionExecutor, internalTransactionExecutor);
            Assert.NotNull(internalHashHelper);
            Assert.Equal(this.state.InternalHashHelper, internalHashHelper);
            Assert.NotNull(message);
            Assert.Equal(this.state.Message, message);
            Assert.NotNull(persistentState);
            Assert.Equal(this.state.PersistentState, persistentState);
            Assert.NotNull(smartContractState);
            Assert.Equal(this.state, smartContractState);
        }

        [Fact]
        public void Constructor_Exists_Tests()
        {
            var parameters = new List<object> { 1 };

            var constructorExists = Contract.ConstructorExists(typeof(TestContract), parameters);

            Assert.True(constructorExists);
        }

        [Fact]
        public void Constructor_Does_Not_Exist_Tests()
        {
            // No constructor with this signature should exist due to missing ISmartContractState
            var parameters = new List<object> { 1, "1" };

            var constructorExists = Contract.ConstructorExists(typeof(TestContract), parameters);

            Assert.False(constructorExists);
        }

        [Fact]
        public void HasFallback_Returns_Correct_Fallback()
        {
            var fallbackContract = Contract.CreateUninitialized(typeof(HasFallback), this.state, this.address);

            var fallbackMethod = ((Contract) fallbackContract).Fallback;

            Assert.NotNull(fallbackMethod);
        }

        [Fact]
        public void HasNoFallback_Returns_Correct_Fallback()
        {
            var fallbackContract = Contract.CreateUninitialized(typeof(HasNoFallback), this.state, this.address);
            var fallbackInstance = (HasNoFallback)fallbackContract.GetPrivateFieldValue("instance");

            // Fallback should be null here because we set the binding flags to only resolve methods on the declared type
            var fallbackMethod = ((Contract)fallbackContract).Fallback;

            Assert.Null(fallbackMethod);
        }

        [Fact]
        public void EmptyMethodName_Invokes_Fallback()
        {
            var fallbackContract = Contract.CreateUninitialized(typeof(HasFallback), this.state, this.address);
            var fallbackInstance = (HasFallback) fallbackContract.GetPrivateFieldValue("instance");
            var methodCall = MethodCall.Fallback();

            var result = fallbackContract.Invoke(methodCall);

            Assert.True(result.IsSuccess);
            Assert.True(fallbackInstance.FallbackInvoked);
        }

        [Fact]
        public void EmptyMethodName_DoesNot_Invoke_Fallback()
        {
            var fallbackContract = Contract.CreateUninitialized(typeof(HasNoFallback), this.state, this.address);
            var methodCall = MethodCall.Fallback();

            var result = fallbackContract.Invoke(methodCall);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void EmptyMethodName_WithParams_DoesNot_Invoke_Fallback()
        {
            var fallbackContract = Contract.CreateUninitialized(typeof(HasFallback), this.state, this.address);
            var fallbackInstance = (HasFallback)fallbackContract.GetPrivateFieldValue("instance");

            var parameters = new object[] { 1, "1" };
            var methodCall = new MethodCall(MethodCall.ExternalFallbackMethodName, parameters);
            var result = fallbackContract.Invoke(methodCall);

            Assert.False(result.IsSuccess);
            Assert.False(fallbackInstance.FallbackInvoked);
            Assert.Equal(ContractInvocationErrorType.MethodDoesNotExist, result.InvocationErrorType);
        }

        [Fact]
        public void Non_Existent_Method_DoesNot_Invoke_Fallback()
        {
            var fallbackContract = Contract.CreateUninitialized(typeof(HasFallback), this.state, this.address);
            var fallbackInstance = (HasFallback)fallbackContract.GetPrivateFieldValue("instance");
            var methodCall = new MethodCall("DoesntExist");

            var result = fallbackContract.Invoke(methodCall);

            Assert.False(result.IsSuccess);
            Assert.False(fallbackInstance.FallbackInvoked);
            Assert.Equal(ContractInvocationErrorType.MethodDoesNotExist, result.InvocationErrorType);
        }

        [Fact]
        public void Invoke_Fallback_Method_Sets_State()
        {
            var methodCall = MethodCall.Fallback();
            var fallbackContract = Contract.CreateUninitialized(typeof(HasFallback), this.state, this.address);
            var fallbackInstance = (HasFallback)fallbackContract.GetPrivateFieldValue("instance");

            fallbackContract.Invoke(methodCall);

            var gasMeter = fallbackInstance.GetBaseTypePrivateFieldValue("gasMeter");
            var block = fallbackInstance.GetBaseTypePrivateFieldValue("Block");
            var getBalance = fallbackInstance.GetBaseTypePrivateFieldValue("getBalance");
            var internalTransactionExecutor = fallbackInstance.GetBaseTypePrivateFieldValue("internalTransactionExecutor");
            var internalHashHelper = fallbackInstance.GetBaseTypePrivateFieldValue("internalHashHelper");
            var message = fallbackInstance.GetBaseTypePrivateFieldValue("Message");
            var persistentState = fallbackInstance.GetBaseTypePrivateFieldValue("PersistentState");
            var smartContractState = fallbackInstance.GetBaseTypePrivateFieldValue("smartContractState");

            Assert.NotNull(gasMeter);
            Assert.Equal(this.state.GasMeter, gasMeter);
            Assert.NotNull(block);
            Assert.Equal(this.state.Block, block);
            Assert.NotNull(getBalance);
            Assert.Equal(this.state.GetBalance, getBalance);
            Assert.NotNull(internalTransactionExecutor);
            Assert.Equal(this.state.InternalTransactionExecutor, internalTransactionExecutor);
            Assert.NotNull(internalHashHelper);
            Assert.Equal(this.state.InternalHashHelper, internalHashHelper);
            Assert.NotNull(message);
            Assert.Equal(this.state.Message, message);
            Assert.NotNull(persistentState);
            Assert.Equal(this.state.PersistentState, persistentState);
            Assert.NotNull(smartContractState);
            Assert.Equal(this.state, smartContractState);
        }
    }
}