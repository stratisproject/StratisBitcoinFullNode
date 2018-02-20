using System;
using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.Exceptions;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    /// <summary>
    /// These tests can be added to/removed from in future should we implement other exceptions
    /// which can possible affect how the caller gets refunded.
    /// </summary>
    public sealed class SmartContractExceptionTests
    {
        private readonly ContractStateRepositoryRoot repository;

        public SmartContractExceptionTests()
        {
            this.repository = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
        }

        [Fact]
        public void VM_Throws_RefundGasException_RefundCorrectAmount()
        {
            var newOptions = new PowConsensusOptions() { MaxBlockWeight = 1500 };
            Network.Main.Consensus.Options = newOptions;

            var consensusLoop = new Mock<IConsensusLoop>();

            var mempool = new Mock<ITxMempool>();

            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowRefundGasExceptionContract.cs");
            var stateRoot = new ContractStateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            stateRoot.SetCode(new uint160(1), contractCode);

            var extendedLoggerFactory = new ExtendedLoggerFactory();
            extendedLoggerFactory.AddConsoleWithFilters();

            var assembler = new SmartContractBlockAssembler(consensusLoop.Object, Network.Main, new MempoolSchedulerLock(), mempool.Object, DateTimeProvider.Default, null,
                extendedLoggerFactory, stateRoot, new SmartContractDecompiler(), new SmartContractValidator(new List<ISmartContractValidator>()), new SmartContractGasInjector(), null);

            var carrier = SmartContractCarrier.CallContract(1, new uint160(1), "ThrowException", 5, (Gas) 100);
            carrier.Sender = new uint160(2);

            var transaction = new Transaction();
            var txMempoolEntry = new TxMempoolEntry(transaction, 1000, DateTimeProvider.Default.GetUtcNow().Ticks, 0, 0, new Money(1000), true, 100, new LockPoints(), newOptions);
            assembler.ExecuteContractFeesAndRefunds(stateRoot, carrier, txMempoolEntry, 0, 0);

            Assert.Equal(550, assembler.fees);
        }

        [Fact]
        public void VM_Throws_OutOfGasException_CanCatch()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowOutOfGasExceptionContract.cs");
            var persistentState = new PersistentState(this.repository, Address.Zero.ToUint160());
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Stratis.SmartContracts.Block(0, 0, 0),
                new Message(Address.Zero, Address.Zero, 0, (Gas) 100),
                1,
                new object[] { }
            );

            SmartContractExecutionResult result = vm.ExecuteMethod(contractCode, "ThrowOutOfGasExceptionContract", "ThrowException", context);
            Assert.Equal(typeof(OutOfGasException), result.Exception.GetType());
        }

        [Fact]
        public void VM_Throws_RefundGasException_CanCatch()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowRefundGasExceptionContract.cs");
            var persistentState = new PersistentState(this.repository, Address.Zero.ToUint160());
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Stratis.SmartContracts.Block(0, 0, 0),
                new Message(Address.Zero, Address.Zero, 0, (Gas) 100),
                1,
                new object[] { }
            );

            SmartContractExecutionResult result = vm.ExecuteMethod(contractCode, "ThrowRefundGasExceptionContract", "ThrowException", context);
            Assert.Equal<ulong>(10, result.GasUnitsUsed);
            Assert.Equal(typeof(RefundGasException), result.Exception.GetType());
        }

        [Fact]
        public void VM_Throws_SystemException_CanCatch()
        {
            byte[] contractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/ThrowSystemExceptionContract.cs");
            var persistentState = new PersistentState(this.repository, Address.Zero.ToUint160());
            var vm = new ReflectionVirtualMachine(persistentState);

            var context = new SmartContractExecutionContext(
                new Stratis.SmartContracts.Block(0, 0, 0),
                new Message(Address.Zero, Address.Zero, 0, (Gas) 100),
                1,
                new object[] { }
            );

            SmartContractExecutionResult result = vm.ExecuteMethod(contractCode, "ThrowSystemExceptionContract", "ThrowException", context);
            Assert.Equal(typeof(Exception), result.Exception.GetType());
        }
    }
}