using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class SmartContractExecutorResultProcessorTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly ISmartContractResultRefundProcessor refundProcessor;

        public SmartContractExecutorResultProcessorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = Network.SmartContractsRegTest;
            this.refundProcessor = new SmartContractResultRefundProcessor(this.loggerFactory);
        }

        [Fact]
        public void ContractExecutionResult_RefundDue_AdjustFee()
        {
            var contractAddress = new uint160(1);

            var carrier = SmartContractCarrier.CallContract(1, contractAddress, "ThrowException", 1, (Gas)5000);
            carrier.Sender = new uint160(2);

            var result = new SmartContractExecutionResult
            {
                GasConsumed = (Gas)950
            };

            this.refundProcessor.Process(result, carrier, new Money(10500));

            Assert.Equal((ulong)6450, result.Fee);
            Assert.Single(result.Refunds);
            Assert.Equal(carrier.Sender.ToBytes(), result.Refunds.First().ScriptPubKey.GetDestination(this.network).ToBytes());
            Assert.Equal(4050, result.Refunds.First().Value);
        }

        [Fact]
        public void ContractExecutionResult_NoRefundDue_NoFeeAdjustment()
        {
            var contractAddress = new uint160(1);

            var carrier = SmartContractCarrier.CallContract(1, contractAddress, "ThrowException", 1, (Gas)5000);
            carrier.Sender = new uint160(2);

            var result = new SmartContractExecutionResult
            {
                GasConsumed = (Gas)5000
            };

            this.refundProcessor.Process(result, carrier, new Money(10500));

            Assert.Equal((ulong)10500, result.Fee);
            Assert.Empty(result.Refunds);
        }

        [Fact]
        public void ContractExecutionResult_OutOfGasException_NoRefundDue_NoFeeAdjustment()
        {
            var contractAddress = new uint160(1);

            var carrier = SmartContractCarrier.CallContract(1, contractAddress, "ThrowException", 1, (Gas)5000);
            carrier.Sender = new uint160(2);

            var result = new SmartContractExecutionResult
            {
                Exception = new OutOfGasException(),
                GasConsumed = (Gas)5000
            };

            this.refundProcessor.Process(result, carrier, new Money(10500));

            Assert.Equal((ulong)10500, result.Fee);
            Assert.Empty(result.Refunds);
        }
    }
}