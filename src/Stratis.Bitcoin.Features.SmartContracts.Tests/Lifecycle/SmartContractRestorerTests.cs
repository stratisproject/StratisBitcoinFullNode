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
    public class SmartContractRestorerTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ISmartContractState state;

        public SmartContractRestorerTests()
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
        public void SmartContract_Restorer_NonpublicStateFieldsSetSuccess()
        {
            LifecycleResult result = SmartContractRestorer
                .Restore(typeof(NoParamContract), this.state);

            Assert.True(result.Success);

            SmartContract contract = result.Object;

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
        public void SmartContract_Restorer_ConstructorNotInvokedSuccess()
        {
            LifecycleResult result = SmartContractRestorer
                .Restore(typeof(ConstructorInvokedContract), this.state);

            Assert.True(result.Success);
            Assert.NotNull(result.Object);
            Assert.False(((ConstructorInvokedContract)result.Object).ConstructorInvoked);
        }
    }
}