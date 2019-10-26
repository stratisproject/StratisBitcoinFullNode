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
        public void IsSpentWithTransactionHavingSpendingDetailsReturnsTrue()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails()
            };

            Assert.True(transaction.IsSpent());
        }

        [Fact]
        public void IsSpentWithTransactionHavingNoSpendingDetailsReturnsFalse()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = null
            };

            Assert.False(transaction.IsSpent());
        }

        [Fact]
        public void UnspentAmountNotConfirmedOnlyGivenNoSpendingDetailsReturnsTransactionAmount()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = null,
                Amount = new Money(15)
            };

            Money result = transaction.GetUnspentAmount(false);

            Assert.Equal(new Money(15), result);
        }

        [Fact]
        public void UnspentAmountNotConfirmedOnlyGivenBeingConfirmedAndSpentConfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails { BlockHeight = 16 },
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.GetUnspentAmount(false);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void UnspentAmountNotConfirmedOnlyGivenBeingConfirmedAndSpentUnconfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails(),
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.GetUnspentAmount(false);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void UnspentAmountConfirmedOnlyGivenBeingConfirmedAndSpentUnconfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails(),
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.GetUnspentAmount(true);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void UnspentAmountNotConfirmedOnlyGivenBeingUnConfirmedAndSpentUnconfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails(),
                Amount = new Money(15),
            };

            Money result = transaction.GetUnspentAmount(false);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void UnspentAmountConfirmedOnlyGivenNoSpendingDetailsReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = null
            };

            Money result = transaction.GetUnspentAmount(true);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void UnspentAmountConfirmedOnlyGivenBeingConfirmedAndSpentConfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails { BlockHeight = 16 },
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.GetUnspentAmount(true);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void UnspentAmountConfirmedOnlyGivenBeingUnConfirmedAndSpentUnconfirmedReturnsZero()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = new SpendingDetails(),
                Amount = new Money(15),
            };

            Money result = transaction.GetUnspentAmount(true);

            Assert.Equal(Money.Zero, result);
        }

        [Fact]
        public void UnspentAmountConfirmedOnlyGivenSpendableAndConfirmedReturnsAmount()
        {
            var transaction = new TransactionData
            {
                SpendingDetails = null,
                Amount = new Money(15),
                BlockHeight = 15
            };

            Money result = transaction.GetUnspentAmount(true);

            Assert.Equal(new Money(15), result);
        }
    }
}
