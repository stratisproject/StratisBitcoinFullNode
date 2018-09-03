using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class TransactionCondenserTests
    {
        private readonly Network network;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Mock<IContractState> contractState;
        private readonly Mock<ISmartContractTransactionContext> context;

        public TransactionCondenserTests()
        {
            this.network = new SmartContractsRegTest();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
            this.contractState = new Mock<IContractState>();
            this.context = new Mock<ISmartContractTransactionContext>();
        }

        [Fact]
        public void ContractHasNoBalance_SendAllToOnePerson()
        {
            uint160 contractAddress = new uint160(1);
            uint160 receiverAddress = new uint160(2);
            var transfers = new List<TransferInfo>
            {
                new TransferInfo
                {
                    From = contractAddress,
                    To = receiverAddress,
                    Value = 100
                }
            };

            this.context.Setup(x => x.TxOutValue).Returns(100);
            this.context.Setup(x => x.TransactionHash).Returns(new uint256(1));
            this.context.Setup(x => x.Nvout).Returns(0);

            var transactionCondenser = new TransactionCondenser(
                contractAddress,
                this.loggerFactory.Object,
                transfers,
                this.contractState.Object,
                this.network,
                this.context.Object
            );

            Transaction condensingTx = transactionCondenser.CreateCondensingTransaction();
            // Assert 1 input and 1 output, from and to are correct.
        }
    }
}
