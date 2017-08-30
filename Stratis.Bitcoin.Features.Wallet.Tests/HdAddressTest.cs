using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Xunit;

namespace Stratis.Bitcoin.Tests.Wallet
{
    public class HdAddressTest
    {
        [Fact]
        public void IsChangeAddressWithValidHdPathForChangeAddressReturnsTrue()
        {
            var address = new HdAddress()
            {
                HdPath = "0/1/2/3/1"
            };

            var result = address.IsChangeAddress();

            Assert.True(result);
        }

        [Fact]
        public void IsChangeAddressWithValidHdPathForNonChangeAddressReturnsFalse()
        {
            var address = new HdAddress()
            {
                HdPath = "0/1/2/3/0"
            };

            var result = address.IsChangeAddress();

            Assert.False(result);
        }

        [Fact]
        public void IsChangeAddressWithTextInHdPathReturnsFalse()
        {
            var address = new HdAddress()
            {
                HdPath = "0/1/2/3/A"
            };

            var result = address.IsChangeAddress();

            Assert.False(result);
        }

        [Fact]
        public void IsChangeAddressWithInvalidHdPathThrowsFormatException()
        {
            Assert.Throws<FormatException>(() =>
            {
                var address = new HdAddress()
                {
                    HdPath = "0/1/2"
                };

                var result = address.IsChangeAddress();
            });
        }

        [Fact]
        public void IsChangeAddressWithEmptyHdPathThrowsArgumentException()
        {
            Assert.Throws<ArgumentException> (() =>
            {
                var address = new HdAddress()
                {
                    HdPath = string.Empty
                };

                var result = address.IsChangeAddress();
            });
        }

        [Fact]
        public void IsChangeAddressWithNulledHdPathThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var address = new HdAddress()
                {
                    HdPath = null
                };

                var result = address.IsChangeAddress();
            });
        }

        [Fact]
        public void UnspentTransactionsWithAddressHavingUnspentTransactionsReturnsUnspentTransactions()
        {
            var address = new HdAddress()
            {
                Transactions = new List<TransactionData>() {
                    new TransactionData() { Id = new uint256(15)},
                    new TransactionData() { Id = new uint256(16), SpendingDetails = new SpendingDetails() },
                    new TransactionData() { Id = new uint256(17)},
                    new TransactionData() { Id = new uint256(18), SpendingDetails = new SpendingDetails() }
                }
            };

            var result = address.UnspentTransactions();

            Assert.Equal(2, result.Count());
            Assert.Equal(new uint256(15), result.ElementAt(0).Id);
            Assert.Equal(new uint256(17), result.ElementAt(1).Id);
        }

        [Fact]
        public void UnspentTransactionsWithAddressNotHavingUnspentTransactionsReturnsEmptyList()
        {
            var address = new HdAddress()
            {
                Transactions = new List<TransactionData>() {
                    new TransactionData() { Id = new uint256(16), SpendingDetails = new SpendingDetails() },
                    new TransactionData() { Id = new uint256(18), SpendingDetails = new SpendingDetails() }
                }
            };

            var result = address.UnspentTransactions();

            Assert.Equal(0, result.Count());
        }

        [Fact]
        public void UnspentTransactionsWithAddressWithoutTransactionsReturnsEmptyList()
        {
            var address = new HdAddress()
            {
                Transactions = new List<TransactionData>()
            };

            var result = address.UnspentTransactions();

            Assert.Equal(0, result.Count());
        }
    }
}
