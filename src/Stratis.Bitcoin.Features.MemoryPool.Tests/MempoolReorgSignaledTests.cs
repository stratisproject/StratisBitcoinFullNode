using System;
using System.Collections.Generic;
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
            var mempoolMock = new Mock<ITxMempool>();
            var mempoolValidatorMock = new Mock<IMempoolValidator>();
            
            var subject = new MempoolReorgSignaled(mempoolMock.Object, mempoolValidatorMock.Object, new MempoolSchedulerLock());

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

            subject.OnNext(nextHeader);

            mempoolValidatorMock.Verify(x => x.AcceptToMemoryPool(It.IsAny<MempoolValidationState>(), transaction1));
            mempoolValidatorMock.Verify(x => x.AcceptToMemoryPool(It.IsAny<MempoolValidationState>(), transaction2));
        }
    }
}
