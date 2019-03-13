using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class TransactionDataTest
    {
        [Fact]
        public void IsConfirmedWithTransactionHavingBlockHeightReturnsTrue()
        {
            var transaction = new TransactionData
            {
                BlockHeight = 15
            };

            Assert.True(transaction.IsConfirmed());
        }

        [Fact]
        public void IsConfirmedWithTransactionHavingNoBlockHeightReturnsFalse()
        {
            var transaction = new TransactionData
            {
                BlockHeight = null
            };

            Assert.False(transaction.IsConfirmed());
        }

        [Fact]
        public void IsSpendableWithTransactionHavingSpendingDetailsReturnsFalse()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails()
            };

            Assert.True(transaction.IsSpent());
        }

        [Fact]
        public void IsSpendableWithTransactionHavingNoSpendingDetailsReturnsTrue()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = null
            };

            Assert.False(transaction.IsSpent());
        }

        [Fact]
        public void SpendableAmountNotConfirmedOnlyGivenNoSpendingDetailsReturnsTransactionAmount()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = null,
                Amount = new Money(15)
            };

            Money result = transaction.SpendableAmount(false);

            Assert.Equal(new Money(15), result);
        }

        [Fact]
        public void SpendableAmountNotConfirmedOnlyGivenBeingConfirmedAndSpentConfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails { BlockHeight = 16 },
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.SpendableAmount(false);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void SpendableAmountNotConfirmedOnlyGivenBeingConfirmedAndSpentUnconfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails(),
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.SpendableAmount(false);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void SpendableAmountConfirmedOnlyGivenBeingConfirmedAndSpentUnconfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails(),
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.SpendableAmount(true);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void SpendableAmountNotConfirmedOnlyGivenBeingUnConfirmedAndSpentUnconfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails(),
                Amount = new Money(15),
            };

            Money result = transaction.SpendableAmount(false);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void SpendableAmountConfirmedOnlyGivenNoSpendingDetailsReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = null
            };

            Money result = transaction.SpendableAmount(true);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void SpendableAmountConfirmedOnlyGivenBeingConfirmedAndSpentConfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails { BlockHeight = 16 },
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.SpendableAmount(true);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void SpendableAmountConfirmedOnlyGivenBeingUnConfirmedAndSpentUnconfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails(),
                Amount = new Money(15),
            };

            Money result = transaction.SpendableAmount(true);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void SpendableAmountConfirmedOnlyGivenSpendableAndConfirmedReturnsAmount()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = null,
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.SpendableAmount(true);

            Assert.Equal(new Money(15), result);
        }
    }
}
