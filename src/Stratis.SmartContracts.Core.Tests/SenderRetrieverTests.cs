using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class SenderRetrieverTests
    {
        private readonly Mock<ICoinView> coinView;
        private readonly Network network;
        private readonly SenderRetriever senderRetriever;

        public SenderRetrieverTests()
        {
            this.coinView = new Mock<ICoinView>();
            this.network = new SmartContractsRegTest();
            this.senderRetriever = new SenderRetriever();
        }

        [Fact]
        public void MissingOutput_Returns_False()
        {
            // Construct transaction.
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn());

            // Setup coinview to return as if the PrevOut does not exist.
            var unspentOutputArray = new UnspentOutputs[0];
            this.coinView.Setup(x => x.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentOutputArray, uint256.Zero));

            var blockTxs = new List<Transaction>();

            // Retriever fails but doesn't throw exception
            GetSenderResult result = this.senderRetriever.GetSender(transaction, this.coinView.Object, blockTxs);
            Assert.False(result.Success);
            Assert.Equal(result.Error, SenderRetriever.OutputsNotInCoinView);
        }

        [Fact]
        public void SpentOutput_Returns_False()
        {
            // Construct transaction.
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(uint256.One, 0)));

            // Setup coinview to return as if the PrevOut is spent.
            var unspentOutputs = new UnspentOutputs();
            unspentOutputs.Outputs = new TxOut[]
            {
                null
            };
            var unspentOutputArray = new UnspentOutputs[]
            {
                unspentOutputs
            };
            this.coinView.Setup(x => x.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentOutputArray, uint256.Zero));

            var blockTxs = new List<Transaction>();

            // Retriever fails but doesn't throw exception
            GetSenderResult result = this.senderRetriever.GetSender(transaction, this.coinView.Object, blockTxs);
            Assert.False(result.Success);
            Assert.Equal(result.Error, SenderRetriever.OutputAlreadySpent);
        }

        [Fact]
        public void InvalidPrevOutIndex_Returns_False()
        {
            // Construct transaction with a reference to prevout index of 2.
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(uint256.One, 2)));

            // Setup coinview to return a prevout with only 1 output. AKA index 2 doesn't exist.
            var unspentOutputs = new UnspentOutputs();
            unspentOutputs.Outputs = new TxOut[]
            {
                new TxOut(0, new Script())
            };
            var unspentOutputArray = new UnspentOutputs[]
            {
                unspentOutputs
            };
            this.coinView.Setup(x => x.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentOutputArray, uint256.Zero));

            var blockTxs = new List<Transaction>();

            // Retriever fails but doesn't throw IndexOutOfRangeException
            GetSenderResult result = this.senderRetriever.GetSender(transaction, this.coinView.Object, blockTxs);
            Assert.False(result.Success);
            Assert.Equal(result.Error, SenderRetriever.InvalidOutputIndex);
        }

        [Fact]
        public void InvalidPrevOutIndex_InsideBlock_Returns_False()
        {
            // Construct transaction with a reference to prevout index of 2.
            Transaction prevOutTransaction = this.network.CreateTransaction();
            prevOutTransaction.AddOutput(0, new Script());
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn(new OutPoint(prevOutTransaction, 2)));

            // Put referenced PrevOut as if it was earlier in the block
            var blockTxs = new List<Transaction>
            {
                prevOutTransaction
            };

            // Retriever fails but doesn't throw IndexOutOfRangeException
            GetSenderResult result = this.senderRetriever.GetSender(transaction, null, blockTxs);
            Assert.False(result.Success);
            Assert.Equal(result.Error, SenderRetriever.InvalidOutputIndex);
        }

        [Fact]
        public void NoCoinViewOrTransactions_Returns_False()
        {
            // Construct transaction.
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn());

            // Retriever fails - no transactions to draw from
            GetSenderResult result = this.senderRetriever.GetSender(transaction, null, null);
            Assert.False(result.Success);
            Assert.Equal(result.Error, SenderRetriever.UnableToGetSender);
        }


    }
}
