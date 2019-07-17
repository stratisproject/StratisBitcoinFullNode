using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests.Wallet
{
    public class MultiSigTransactionsTests
    {
        /// <summary>
        /// This class creates all valid <c>true</c>/<c>false</c> combinations for input to <see cref="TransactionDataAddedToMultiSigTransactionsExistsInExpectedLookups(bool, bool, bool, bool, bool, bool)"/>.
        /// </summary>
        private class TestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                for (int mask = 0; mask < 64; mask++)
                {
                    bool hasBlockHeight = (mask ^ 32) != 0;
                    bool hasSpendingDetails = (mask & 16) != 0;
                    bool hasWithdrawalDetails = (mask & 8) != 0;
                    bool flipBlockHeight = (mask & 4) != 0;
                    bool flipSpendingDetails = (mask & 2) != 0;
                    bool flipWithdrawalDetails = (mask & 1) != 0;

                    // Can't have withdrawal details without spending details.
                    if (!hasSpendingDetails && hasWithdrawalDetails)
                        continue;

                    if (!(hasSpendingDetails ^ flipSpendingDetails) && (hasWithdrawalDetails ^ flipWithdrawalDetails))
                        continue;

                    yield return new object[] { hasBlockHeight, hasSpendingDetails, hasWithdrawalDetails, flipBlockHeight, flipSpendingDetails, flipWithdrawalDetails };
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void CanChangeHeightOfSpendableTransaction()
        {
            var transactionData1 = new TransactionData()
            {
                Id = 1,
                Index = 1,
                BlockHeight = null,
                SpendingDetails = null
            };

            var transactionData2 = new TransactionData()
            {
                Id = 2,
                Index = 0,
                BlockHeight = null,
                SpendingDetails = null
            };

            var transactions = new MultiSigTransactions();
            transactions.Add(transactionData2);
            transactions.Add(transactionData1);

            transactionData2.BlockHeight = 1;

            transactions.Remove(transactionData2);

            transactionData2.BlockHeight = null;

            transactions.Add(transactionData2);
        }

        [Theory]
        [ClassData(typeof(TestData))]
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
                    BlockHeight = spendingBlockHeight(),
                    TransactionId = spendingTransactionId
                };
            }

            int? blockHeight()
            {
                return hasBlockHeight ? 3 : (int?)null;
            }

            int? spendingBlockHeight()
            {
                return hasBlockHeight ? 4 : (int?)null;
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
                    Assert.Single(transactions.SpentTransactionsBeforeHeight(int.MaxValue), x => x.Item1 == spendingBlockHeight());
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

            Assert.Empty(transactions);
            Assert.Empty(transactions.SpentTransactionsBeforeHeight(int.MaxValue));
            Assert.Empty(transactions.GetUnspentTransactions());
            Assert.Empty(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).Single().txList);
        }
    }
}
