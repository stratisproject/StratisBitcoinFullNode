using System;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.State;
using Xunit;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    /// <summary>
    /// These tests can be added to/removed from in future should we implement other exceptions
    /// which can possible affect how the caller gets refunded.
    /// </summary>
    public sealed class SmartContractExceptionTests
    {
        private readonly ContractStateRepositoryRoot repository;
        private readonly Network network;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private static readonly Address TestAddress = (Address) "mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";

        public SmartContractExceptionTests()
        {
            this.repository =
                new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.network = Network.SmartContractsRegTest;
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
        }

        [Fact]
        public void VM_Throws_Exception_CanCatch()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/ThrowExceptionContract.cs");
            Assert.True(compilationResult.Success);

            byte[] contractCode = compilationResult.Compilation;

            var gasLimit = (Gas) 100;
            var gasMeter = new GasMeter(gasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy,
                TestAddress.ToUint160(this.network), this.network);
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Block(0, TestAddress),
                new Message(TestAddress, TestAddress, 0, gasLimit),
                1,
                new object[] { }
            );

            var internalTransactionExecutor = new InternalTransactionExecutor(this.repository, this.network, this.keyEncodingStrategy);
            Func<ulong> getBalance = () => this.repository.GetCurrentBalance(TestAddress.ToUint160(this.network));

            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode,
                "ThrowException",
                context,
                gasMeter,
                internalTransactionExecutor,
                getBalance);

            Assert.Equal(typeof(Exception), result.Exception.GetType());
        }
    }
}