using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class SmartContractResultRefundProcessorTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly ISmartContractResultRefundProcessor refundProcessor;

        public SmartContractResultRefundProcessorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = new SmartContractsRegTest();
            this.refundProcessor = new SmartContractResultRefundProcessor(this.loggerFactory);
        }

        [Fact]
        public void ContractExecutionResult_RefundDue_AdjustFee()
        {
            var contractAddress = new uint160(1);

            var carrier = SmartContractCarrier.CallContract(1, contractAddress, "ThrowException", 1, (Gas)5000);
            carrier.Sender = new uint160(2);

            (var fee, var refunds) = this.refundProcessor.Process(carrier.ContractTxData, new Money(10500), carrier.Sender, (Gas)950, null);

            Assert.Equal(6450, fee);
            Assert.Single(refunds);
            Assert.Equal(carrier.Sender.ToBytes(), refunds.First().ScriptPubKey.GetDestination(this.network).ToBytes());
            Assert.Equal(4050, refunds.First().Value);
        }

        [Fact]
        public void ContractExecutionResult_NoRefundDue_NoFeeAdjustment()
        {
            var contractAddress = new uint160(1);

            var carrier = SmartContractCarrier.CallContract(1, contractAddress, "ThrowException", 1, (Gas)5000);
            carrier.Sender = new uint160(2);

            (var fee, var refunds) = this.refundProcessor.Process(carrier.ContractTxData, new Money(10500), carrier.Sender, (Gas)5000, null);

            Assert.Equal(10500, fee);
            Assert.Empty(refunds);
        }

        [Fact]
        public void ContractExecutionResult_OutOfGasException_NoRefundDue_NoFeeAdjustment()
        {
            var contractAddress = new uint160(1);

            var carrier = SmartContractCarrier.CallContract(1, contractAddress, "ThrowException", 1, (Gas)5000);
            carrier.Sender = new uint160(2);

            (var fee, var refunds) = this.refundProcessor.Process(carrier.ContractTxData, new Money(10500), carrier.Sender, (Gas)5000, new OutOfGasException());

            Assert.Equal(10500, fee);
            Assert.Empty(refunds);
        }
    }
}