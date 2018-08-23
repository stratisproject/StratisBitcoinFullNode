using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ContractUpgradeabilityTests
    {
        public class TestAlc : AssemblyLoadContext
        {
            protected override Assembly Load(AssemblyName assemblyName)
            {
                return null;
            }
        }

        [Fact]
        public void UpgradeContract()
        {
            var source = @"
using Stratis.SmartContracts;

public TestContract : SmartContract
{
    public TestContract(ISmartContractState state) : base(state) {}
    
    public void TestMethod() {}
}
";
            var version1DllPath = Path.Combine("Packages", "1.0.0.0-TEST", "Stratis.SmartContracts.dll");
            var version2DllPath = Path.Combine("Packages", "2.0.0.0-TEST", "Stratis.SmartContracts.dll");

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            CSharpCompilation compilation = CSharpCompilation.Create(
                "smartContract",
                new[] { syntaxTree },
                new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(version1DllPath),
                },
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    checkOverflow: true));


            using (var dllStream = new MemoryStream())
            {
                EmitResult emitResult = compilation.Emit(dllStream);

                dllStream.ToArray();
            }

            var alc = new TestAlc();


            // Compile earlier version of Stratis.SmartContracts?
            // Compile contract against earlier version
            // Create new isolated ALC
            // Load new Stratis.SmartContracts into ALC
            // Load earlier contract into new ALC
        }
    }

    public class ContractTests
    {
        private ISmartContractState state;
        private TestContract instance;
        private Type type;
        private uint160 address;
        private IContract contract;

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
            this.instance = (TestContract) this.GetPrivateFieldValue(this.contract, "instance");
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
            IContractInvocationResult result = this.contract.Invoke("Test1", null);

            Assert.True(result.IsSuccess);
            Assert.True(this.instance.Test1Called);
            Assert.Equal(0, this.instance.ConstructorCalledCount);
        }

        [Fact]
        public void Invoke_Method_With_One_Params()
        {
            var param = 9999;
            IContractInvocationResult result = this.contract.Invoke("Test2", new List<object>() { param });

            Assert.True(result.IsSuccess);
            Assert.Equal(param, result.Return);
        }

        [Fact]
        public void Invoke_Private_Method_Fails()
        {
            IContractInvocationResult result = this.contract.Invoke("TestPrivate", new List<object>());

            Assert.False(result.IsSuccess);
            Assert.Equal(ContractInvocationErrorType.MethodDoesNotExist, result.InvocationErrorType);
        }

        [Fact]
        public void Invoke_Method_Throws_Exception()
        {
            IContractInvocationResult result = this.contract.Invoke("TestException", new List<object>());

            Assert.False(result.IsSuccess);
            Assert.Equal(ContractInvocationErrorType.MethodThrewException, result.InvocationErrorType);
        }

        [Fact]
        public void Invoke_Method_Throws_OutOfGasException()
        {
            IContractInvocationResult result = this.contract.Invoke("TestOutOfGas", new List<object>());

            Assert.False(result.IsSuccess);
            Assert.Equal(ContractInvocationErrorType.OutOfGas, result.InvocationErrorType);
        }

        [Fact]
        public void Invoke_Method_Sets_State()
        {
            this.contract.Invoke("Test1", null);

            var gasMeter = this.GetInstancePrivateFieldValue("gasMeter");
            var block = this.GetInstancePrivateFieldValue("Block");
            var getBalance = this.GetInstancePrivateFieldValue("getBalance");
            var internalTransactionExecutor = this.GetInstancePrivateFieldValue("internalTransactionExecutor");
            var internalHashHelper = this.GetInstancePrivateFieldValue("internalHashHelper");
            var message = this.GetInstancePrivateFieldValue("Message");
            var persistentState = this.GetInstancePrivateFieldValue("PersistentState");
            var smartContractState = this.GetInstancePrivateFieldValue("smartContractState");

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

        private object GetInstancePrivateFieldValue(string fieldName)
        {
            var field = this.instance.GetType().BaseType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field.GetValue(this.instance);
        }

        private object GetPrivateFieldValue(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field.GetValue(obj);
        }
    }
}