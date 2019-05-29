using NBitcoin;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests.Wallet
{
    public class MultiSigTransactionsTests
    {
        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(false, false, false, true)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, true, true)]
        [InlineData(false, true, false, false)]
        [InlineData(false, true, false, true)]
        [InlineData(false, true, true, false)]
        [InlineData(false, true, true, true)]
        [InlineData(true, false, false, false)]
        [InlineData(true, false, false, true)]
        [InlineData(true, false, true, false)]
        [InlineData(true, false, true, true)]
        [InlineData(true, true, false, false)]
        [InlineData(true, true, false, true)]
        [InlineData(true, true, true, false)]
        [InlineData(true, true, true, true)]
        public void TransactionDataAddedToMultiSigTransactionsExistsInExpectedLookups(bool hasBlockHeight, bool hasSpendingDetails, bool flipBlockHeight, bool flipSpendingDetails)
        {
            uint256 transactionId = 1;
            int transactionIndex = 2;
            uint256 spendingTransactionId = 2;

            SpendingDetails spendingDetails() { return hasSpendingDetails ? new SpendingDetails() { TransactionId = spendingTransactionId } : null; }
            int? blockHeight() { return hasBlockHeight ? 3 : (int?)null; }

            var transactionData = new TransactionData()
            {
                Id = transactionId,
                Index = transactionIndex,
                BlockHeight = blockHeight(),
                SpendingDetails = spendingDetails()
            };

            var transactions = new MultiSigTransactions();
            transactions.Add(transactionData);

            Assert.Contains(transactionData, transactions);

            void Validate()
            {
                if (hasBlockHeight && hasSpendingDetails)
                    Assert.Single(transactions.SpentTransactionsBeforeHeight(int.MaxValue), x => x.Item1 == blockHeight());
                else
                    Assert.Empty(transactions.SpentTransactionsBeforeHeight(int.MaxValue));

                if (hasSpendingDetails)
                    Assert.Empty(transactions.GetUnspentTransactions());
                else
                    Assert.Single(transactions.GetUnspentTransactions(), x => x.BlockHeight == blockHeight());
            }

            Validate();

            hasBlockHeight ^= flipBlockHeight;
            hasSpendingDetails ^= hasSpendingDetails;

            transactionData.BlockHeight = blockHeight();
            transactionData.SpendingDetails = spendingDetails();

            Validate();

            transactions.Remove(transactionData);

            Assert.DoesNotContain(transactionData, transactions);
        }
    }
}
