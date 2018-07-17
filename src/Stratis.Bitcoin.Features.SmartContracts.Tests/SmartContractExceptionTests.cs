using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
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
        private readonly ILoggerFactory loggerFactory;
        private SmartContractValidator validator;
        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";
        
        public SmartContractExceptionTests()
        {
            this.repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = new SmartContractsRegTest();
            this.validator = new SmartContractValidator(new List<ISmartContractValidator>());
        }

        [Fact]
        public void VM_Throws_Exception_CanCatch()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/ThrowExceptionContract.cs");
            Assert.True(compilationResult.Success);

            byte[] contractCode = compilationResult.Compilation;

            var gasLimit = (Gas)100;
            var gasMeter = new GasMeter(gasLimit);
            var persistenceStrategy = new MeteredPersistenceStrategy(this.repository, gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy,
                TestAddress.ToUint160(this.network), this.network);
            var internalTxExecutorFactory = new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network);
            var vm = new ReflectionVirtualMachine(this.validator, internalTxExecutorFactory, this.loggerFactory, this.network);

            var address = TestAddress.ToUint160(this.network);

            var callData = new CallData(1, 1, gasLimit, contractCode);

            var transactionContext = new TransactionContext(uint256.One, 0, address, address, 0);

            var result = vm.ExecuteMethod(gasMeter, persistentState, 
                this.repository, 
                callData, 
                transactionContext);

            Assert.Equal(typeof(Exception), result.ExecutionException.GetType());
        }
    }
}