using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class HdAddressTest
    {
        [Fact]
        public void IsChangeAddressWithValidHdPathForChangeAddressReturnsTrue()
        {
            var address = new HdAddress
            {
                HdPath = "0/1/2/3/1"
            };

            bool result = address.IsChangeAddress();

            Assert.True(result);
        }

        [Fact]
        public void IsChangeAddressWithValidHdPathForNonChangeAddressReturnsFalse()
        {
            var address = new HdAddress
            {
                HdPath = "0/1/2/3/0"
            };

            bool result = address.IsChangeAddress();

            Assert.False(result);
        }

        [Fact]
        public void IsChangeAddressWithTextInHdPathReturnsFalse()
        {
            var address = new HdAddress
            {
                HdPath = "0/1/2/3/A"
            };

            bool result = address.IsChangeAddress();

            Assert.False(result);
        }

        [Fact]
        public void IsChangeAddressWithInvalidHdPathThrowsFormatException()
        {
            Assert.Throws<FormatException>(() =>
            {
                var address = new HdAddress
                {
                    HdPath = "0/1/2"
                };

                bool result = address.IsChangeAddress();
            });
        }

        [Fact]
        public void IsChangeAddressWithEmptyHdPathThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
           {
               var address = new HdAddress
               {
                   HdPath = string.Empty
               };

               bool result = address.IsChangeAddress();
           });
        }

        [Fact]
        public void IsChangeAddressWithNulledHdPathThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var address = new HdAddress
                {
                    HdPath = null
                };

                bool result = address.IsChangeAddress();
            });
        }

        [Fact]
        public void UnspentTransactionsWithAddressHavingUnspentTransactionsReturnsUnspentTransactions()
        {
            var address = new HdAddress
            {
                Transactions = new List<TransactionData> {
                    new TransactionData { Id = new uint256(15)},
                    new TransactionData { Id = new uint256(16), SpendingDetails = new SpendingDetails() },
                    new TransactionData { Id = new uint256(17)},
                    new TransactionData { Id = new uint256(18), SpendingDetails = new SpendingDetails() }
                }
            };

            IEnumerable<TransactionData> result = address.UnspentTransactions();

            Assert.Equal(2, result.Count());
            Assert.Equal(new uint256(15), result.ElementAt(0).Id);
            Assert.Equal(new uint256(17), result.ElementAt(1).Id);
        }

        [Fact]
        public void UnspentTransactionsWithAddressNotHavingUnspentTransactionsReturnsEmptyList()
        {
            var address = new HdAddress
            {
                Transactions = new List<TransactionData> {
                    new TransactionData { Id = new uint256(16), SpendingDetails = new SpendingDetails() },
                    new TransactionData { Id = new uint256(18), SpendingDetails = new SpendingDetails() }
                }
            };

            IEnumerable<TransactionData> result = address.UnspentTransactions();

            Assert.Empty(result);
        }

        [Fact]
        public void UnspentTransactionsWithAddressWithoutTransactionsReturnsEmptyList()
        {
            var address = new HdAddress
            {
                Transactions = new List<TransactionData>()
            };

            IEnumerable<TransactionData> result = address.UnspentTransactions();

            Assert.Empty(result);
        }
    }
}
