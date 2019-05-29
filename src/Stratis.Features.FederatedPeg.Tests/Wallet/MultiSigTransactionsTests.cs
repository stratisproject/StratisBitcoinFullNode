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
        private class TestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                for (int mask = 0; mask < 64; mask++)
                {
                    yield return new object[] {
                        (mask & 32) != 0,
                        (mask & 16) != 0,
                        (mask & 8) != 0,
                        (mask & 4) != 0,
                        (mask & 2) != 0,
                        (mask & 1) != 0
                    };
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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

            Assert.Empty(transactions);
            Assert.Empty(transactions.SpentTransactionsBeforeHeight(int.MaxValue));
            Assert.Empty(transactions.GetUnspentTransactions());
            Assert.Empty(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).Single().txList);
        }
    }
}
