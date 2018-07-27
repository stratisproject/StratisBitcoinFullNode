using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SmartContractResultTransferProcessorTests
    {
        private readonly Network network;
        private readonly ILoggerFactory loggerFactory;
        private readonly SmartContractResultTransferProcessor transferProcessor;

        public SmartContractResultTransferProcessorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = new SmartContractsRegTest();
            this.transferProcessor = new SmartContractResultTransferProcessor(this.loggerFactory, this.network);
        }

        [Fact]
        public void TransferProcessor_NoBalance_NoTransfers()
        {
            // Scenario where contract was sent 0, doesn't yet have any UTXO assigned, and no transfers were made.
            var stateMock = new Mock<IContractStateRepository>();
            stateMock.Setup(x => x.GetCode(It.IsAny<uint160>())).Returns<byte[]>(null);
            var txContextMock = new Mock<ISmartContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);
            var result = new SmartContractExecutionResult();
            this.transferProcessor.Process(stateMock.Object, uint160.One, txContextMock.Object, new List<TransferInfo>(), false);

            // Ensure no state changes were made and no transaction has been added
            Assert.Null(result.InternalTransaction);
        }

        [Fact]
        public void TransferProcessor_NoBalance_ReceivedFunds()
        {
            // Scenario where contract was sent some funds, doesn't yet have any UTXO assigned, and no transfers were made.
            var stateMock = new Mock<IContractStateRepository>();
            stateMock.Setup(x => x.GetCode(It.IsAny<uint160>())).Returns<byte[]>(null);
            var txContextMock = new Mock<ISmartContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(100);
            var result = new SmartContractExecutionResult();

            this.transferProcessor.Process(stateMock.Object, uint160.One, txContextMock.Object, new List<TransferInfo>(), false);
            
            // Ensure unspent was saved, but no condensing transaction was generated.
            Assert.Null(result.InternalTransaction);
            stateMock.Verify(x => x.SetUnspent(new uint160(1), It.IsAny<ContractUnspentOutput>()));
        }

        // TODO: Test to simulate scenario in which condensing transaction is generated.
    }
}
