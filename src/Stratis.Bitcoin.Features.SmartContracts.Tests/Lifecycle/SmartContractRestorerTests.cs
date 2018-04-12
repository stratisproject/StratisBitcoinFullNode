using System.Reflection;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.Lifecycle;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Lifecycle
{
    public class SmartContractRestorerTests
    {
        private readonly ISmartContractState State = new SmartContractState(
            new Block(0, new Address()),
            new Message(new Address(), new Address(), 0, (Gas)0),
            new PersistentState(null, 0, null),
            new GasMeter((Gas)1000),
            new InternalTransactionExecutor(null, null, new BasicKeyEncodingStrategy(), null),
            new InternalHashHelper(),
            () => 10000);

        [Fact]
        public void SmartContract_Restorer_NonpublicStateFieldsSetSuccess()
        {
            LifecycleResult result = SmartContractRestorer
                .Restore(typeof(NoParamContract), this.State);

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
                        Assert.Equal(this.State.Block, value);
                        break;
                    case "Message":
                        Assert.Equal(this.State.Message, value);
                        break;
                    case "PersistentState":
                        Assert.Equal(this.State.PersistentState, value);
                        break;
                    case "gasMeter":
                        Assert.Equal(this.State.GasMeter, value);
                        break;
                    case "getBalance":
                        Assert.Equal(this.State.GetBalance, value);
                        break;
                    case "internalTransactionExecutor":
                        Assert.Equal(this.State.InternalTransactionExecutor, value);
                        break;
                    case "internalHashHelper":
                        Assert.Equal(this.State.InternalHashHelper, value);
                        break;
                    case "smartContractState":
                        Assert.Equal(this.State, value);
                        break;
                }
            }
        }

        [Fact]
        public void SmartContract_Restorer_ConstructorNotInvokedSuccess()
        {
            LifecycleResult result = SmartContractRestorer
                .Restore(typeof(ConstructorInvokedContract), this.State);

            Assert.True(result.Success);
            Assert.NotNull(result.Object);
            Assert.False(((ConstructorInvokedContract)result.Object).ConstructorInvoked);
        }        
    }
}