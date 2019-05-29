using System;
using System.Linq;
using NBitcoin;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests.Wallet
{
    public class MultiSigTransactionsTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TransactionDataAddedToMultiSigTransactionsExistsInExpectedLookups(bool hasBlockHeight, bool hasSpendingDetails)
        {
            uint256 transactionId = 1;
            int transactionIndex = 2;
            uint256 spendingTransactionId = 2;

            SpendingDetails spendingDetails = hasSpendingDetails ? new SpendingDetails() { TransactionId = spendingTransactionId } : null;
            int? blockHeight = hasBlockHeight ? 3 : (int?)null;

            var transactionData = new TransactionData()
            {
                Id = transactionId,
                Index = transactionIndex,
                BlockHeight = blockHeight,
                SpendingDetails = spendingDetails
            };

            var transactions = new MultiSigTransactions();
            transactions.Add(transactionData);

            Assert.Contains(transactionData, transactions);

            if (hasBlockHeight && hasSpendingDetails)
                Assert.Single(transactions.SpentTransactionsBeforeHeight(int.MaxValue), x => x.Item1 == blockHeight);
            else
                Assert.Empty(transactions.SpentTransactionsBeforeHeight(int.MaxValue));

            if (hasSpendingDetails)
                Assert.Empty(transactions.GetUnspentTransactions());
            else
                Assert.Single(transactions.GetUnspentTransactions(), x => x.BlockHeight == blockHeight);
        }
    }
}
