using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    public class MempoolReorgSignaledTests
    {
        [Fact]
        public void OnNextCore_WhenTransactionsMissingInLongestChain_ReturnsThemToTheMempool()
        {
            var mempoolValidatorMock = new Mock<IMempoolValidator>();
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(i => i.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

            var subject = new BlocksDisconnectedSignaled(mempoolValidatorMock.Object, new MempoolSchedulerLock(), loggerFactoryMock.Object);

            ChainedHeader genesisHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();

            var blockHeader = new BlockHeader();
            blockHeader.HashPrevBlock = genesisHeader.HashBlock;

            var nextHeader = new ChainedHeader(blockHeader, blockHeader.GetHash(), genesisHeader);
            nextHeader.Block = new Block();
            nextHeader.Block.Transactions = new List<Transaction>();
            var transaction1 = new Transaction();
            var transaction2 = new Transaction();
            nextHeader.Block.Transactions.Add(transaction1);
            nextHeader.Block.Transactions.Add(transaction2);

            subject.OnNext(nextHeader.Block);

            mempoolValidatorMock.Verify(x => x.AcceptToMemoryPool(It.IsAny<MempoolValidationState>(), transaction1));
            mempoolValidatorMock.Verify(x => x.AcceptToMemoryPool(It.IsAny<MempoolValidationState>(), transaction2));
        }
    }
}
