using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.SmartContracts.CLR.ResultProcessors;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public sealed class ContractRefundProcessorTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly IContractRefundProcessor refundProcessor;

        public ContractRefundProcessorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = new SmartContractsRegTest();
            this.refundProcessor = new ContractRefundProcessor(this.loggerFactory);
        }

        [Fact]
        public void ContractExecutionResult_RefundDue_AdjustFee()
        {
            var contractAddress = new uint160(1);

            var contractTxData = new ContractTxData(1, 1, (Gas)5000, contractAddress, "ThrowException");
            var sender = new uint160(2);

            (Money fee, TxOut refund) = this.refundProcessor.Process(contractTxData, new Money(10500), sender, (Gas)950, false);

            Assert.Equal(6450, fee);
            Assert.Equal(sender.ToBytes(), refund.ScriptPubKey.GetDestination(this.network).ToBytes());
            Assert.Equal(4050, refund.Value);
        }

        [Fact]
        public void ContractExecutionResult_NoRefundDue_NoFeeAdjustment()
        {
            var contractAddress = new uint160(1);

            var contractTxData = new ContractTxData(1, 1, (Gas)5000, contractAddress, "ThrowException");
            var sender = new uint160(2);

            (Money fee, TxOut refund) = this.refundProcessor.Process(contractTxData, new Money(10500), sender, (Gas)5000, false);

            Assert.Equal(10500, fee);
            Assert.Null(refund);
        }

        [Fact]
        public void ContractExecutionResult_OutOfGasException_NoRefundDue_NoFeeAdjustment()
        {
            var contractAddress = new uint160(1);

            var contractTxData = new ContractTxData(1, 1, (Gas)5000, contractAddress, "ThrowException");
            var sender = new uint160(2);

            (Money fee, TxOut refund) = this.refundProcessor.Process(contractTxData, new Money(10500), sender, (Gas)5000, true);

            Assert.Equal(10500, fee);
            Assert.Null(refund);
        }
    }
}