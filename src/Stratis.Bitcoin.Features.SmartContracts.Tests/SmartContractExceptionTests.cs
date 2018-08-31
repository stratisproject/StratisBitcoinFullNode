using System;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    /// <summary>
    /// These tests can be added to/removed from in future should we implement other exceptions
    /// which can possible affect how the caller gets refunded.
    /// </summary>
    public sealed class SmartContractExceptionTests
    {
        private static readonly Address TestAddress = (Address)"mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn";

        private readonly Network network;
        private readonly ContractStateRoot repository;
        private readonly ReflectionVirtualMachine vm;

        public SmartContractExceptionTests()
        {
            var context = new ContractExecutorTestContext();
            this.network = context.Network;
            this.repository = context.State;
            this.vm = context.Vm;
        }

        [Fact]
        public void VM_Throws_Exception_CanCatch()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/ThrowExceptionContract.cs");
            Assert.True(compilationResult.Success);

            byte[] contractCode = compilationResult.Compilation;

            var gasLimit = (Gas)10000;
            var gasMeter = new GasMeter(gasLimit);

            uint160 address = TestAddress.ToUint160(this.network);

            var callData = new CallData(gasLimit, address, "ThrowException");
            this.repository.SetCode(address, contractCode);
            this.repository.SetContractType(address, "ThrowExceptionContract");
            var transactionContext = new TransactionContext(uint256.One, 0, address, address, 0);

            VmExecutionResult result = this.vm.ExecuteMethod(gasMeter,
                this.repository,
                callData,
                transactionContext);

            Assert.Equal(typeof(Exception), result.ExecutionException.GetType());
        }
    }
}