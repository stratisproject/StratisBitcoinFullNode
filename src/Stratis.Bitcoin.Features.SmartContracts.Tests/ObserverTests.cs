using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using RuntimeObserver;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.ILRewrite;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ObserverTests
    {
        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";
        private TestSmartContractState smartContractState;
        private const ulong Balance = 0;
        private const ulong GasLimit = 10000;
        private const ulong Value = 0;

        public ObserverTests()
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
        public void CanInject()
        {
            SmartContractCompilationResult assembly = SmartContractCompiler.CompileFile("SmartContracts/Auction.cs");
            ModuleDefinition module = ModuleDefinition.ReadModule(new MemoryStream(assembly.Compilation));
            Guid observerId = AssemblyRewriter.RewriteModule(module);
            var moduleMem = new MemoryStream();
            module.Write(moduleMem);

            var gasMeter = new GasMeter((Gas) 100); 

            ObserverInstances.Set(observerId, new Observer(gasMeter));

            Assembly loadedAssembly = Assembly.Load(moduleMem.ToArray());
            Type type = loadedAssembly.GetType("Auction");

            object auction = Activator.CreateInstance(type, new object[] { this.smartContractState, (ulong) 20 });

            var observer = ObserverInstances.Get(observerId.ToString());
        }

    }
}
