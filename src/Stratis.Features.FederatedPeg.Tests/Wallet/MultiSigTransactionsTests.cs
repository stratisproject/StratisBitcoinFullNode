using System.Linq;
using NBitcoin;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests.Wallet
{
    public class MultiSigTransactionsTests
    {
        // TODO: Update this to create a MultiSigTransactions with 64 different TransactionData entries.
        [Theory]
        [InlineData(false, false, false, false, false, false)]
        [InlineData(false, false, false, false, false, true)]
        [InlineData(false, false, false, false, true, false)]
        [InlineData(false, false, false, false, true, true)]
        [InlineData(false, false, false, true, false, false)]
        [InlineData(false, false, false, true, false, true)]
        [InlineData(false, false, false, true, true, false)]
        [InlineData(false, false, false, true, true, true)]
        [InlineData(false, false, true, false, false, false)]
        [InlineData(false, false, true, false, false, true)]
        [InlineData(false, false, true, false, true, false)]
        [InlineData(false, false, true, false, true, true)]
        [InlineData(false, false, true, true, false, false)]
        [InlineData(false, false, true, true, false, true)]
        [InlineData(false, false, true, true, true, false)]
        [InlineData(false, false, true, true, true, true)]
        [InlineData(false, true, false, false, false, false)]
        [InlineData(false, true, false, false, false, true)]
        [InlineData(false, true, false, false, true, false)]
        [InlineData(false, true, false, false, true, true)]
        [InlineData(false, true, false, true, false, false)]
        [InlineData(false, true, false, true, false, true)]
        [InlineData(false, true, false, true, true, false)]
        [InlineData(false, true, false, true, true, true)]
        [InlineData(false, true, true, false, false, false)]
        [InlineData(false, true, true, false, false, true)]
        [InlineData(false, true, true, false, true, false)]
        [InlineData(false, true, true, false, true, true)]
        [InlineData(false, true, true, true, false, false)]
        [InlineData(false, true, true, true, false, true)]
        [InlineData(false, true, true, true, true, false)]
        [InlineData(false, true, true, true, true, true)]
        [InlineData(true, false, false, false, false, false)]
        [InlineData(true, false, false, false, false, true)]
        [InlineData(true, false, false, false, true, false)]
        [InlineData(true, false, false, false, true, true)]
        [InlineData(true, false, false, true, false, false)]
        [InlineData(true, false, false, true, false, true)]
        [InlineData(true, false, false, true, true, false)]
        [InlineData(true, false, false, true, true, true)]
        [InlineData(true, false, true, false, false, false)]
        [InlineData(true, false, true, false, false, true)]
        [InlineData(true, false, true, false, true, false)]
        [InlineData(true, false, true, false, true, true)]
        [InlineData(true, false, true, true, false, false)]
        [InlineData(true, false, true, true, false, true)]
        [InlineData(true, false, true, true, true, false)]
        [InlineData(true, false, true, true, true, true)]
        [InlineData(true, true, false, false, false, false)]
        [InlineData(true, true, false, false, false, true)]
        [InlineData(true, true, false, false, true, false)]
        [InlineData(true, true, false, false, true, true)]
        [InlineData(true, true, false, true, false, false)]
        [InlineData(true, true, false, true, false, true)]
        [InlineData(true, true, false, true, true, false)]
        [InlineData(true, true, false, true, true, true)]
        [InlineData(true, true, true, false, false, false)]
        [InlineData(true, true, true, false, false, true)]
        [InlineData(true, true, true, false, true, false)]
        [InlineData(true, true, true, false, true, true)]
        [InlineData(true, true, true, true, false, false)]
        [InlineData(true, true, true, true, false, true)]
        [InlineData(true, true, true, true, true, false)]
        [InlineData(true, true, true, true, true, true)]
        public void TransactionDataAddedToMultiSigTransactionsExistsInExpectedLookups(bool hasBlockHeight, bool hasSpendingDetails, bool hasWithdrawalDetails, bool flipBlockHeight, bool flipSpendingDetails, bool flipWithdrawalDetails)
        {
            uint256 transactionId = 1;
            int transactionIndex = 2;
            uint256 spendingTransactionId = 2;
            uint256 spendingDepositId = 3;

            SpendingDetails spendingDetails()
            {
                if (!hasSpendingDetails)
                    return null;

                return new SpendingDetails() {
                    WithdrawalDetails = hasWithdrawalDetails ? new WithdrawalDetails() {
                         MatchingDepositId = spendingDepositId
                    } : null,
                    TransactionId = spendingTransactionId
                };
            }

            int? blockHeight()
            {
                return hasBlockHeight ? 3 : (int?)null;
            }

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

                if (hasSpendingDetails && hasWithdrawalDetails)
                    Assert.Single(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).First().txList);
                else
                    Assert.Empty(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).First().txList);

                if (hasSpendingDetails)
                    Assert.Empty(transactions.GetUnspentTransactions());
                else
                    Assert.Single(transactions.GetUnspentTransactions(), x => x.BlockHeight == blockHeight());
            }

            Validate();

            hasBlockHeight ^= flipBlockHeight;
            hasSpendingDetails ^= flipSpendingDetails;
            hasWithdrawalDetails ^= flipWithdrawalDetails;

            transactionData.BlockHeight = blockHeight();
            transactionData.SpendingDetails = spendingDetails();

            Validate();

            transactions.Remove(transactionData);

            Assert.DoesNotContain(transactionData, transactions);

            Assert.Empty(transactions.SpentTransactionsBeforeHeight(int.MaxValue));
            Assert.Empty(transactions.GetUnspentTransactions());
            Assert.Empty(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).First().txList);
        }
    }
}
