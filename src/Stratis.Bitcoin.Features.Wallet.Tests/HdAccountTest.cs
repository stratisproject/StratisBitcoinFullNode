using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class HdAccountTest
    {
        [Fact]
        public void GetCoinTypeHavingHdPathReturnsCointType()
        {
            var account = new HdAccount();
            account.HdPath = "1/2/105";

            CoinType result = account.GetCoinType();

            Assert.Equal(CoinType.Stratis, result);
        }

        [Fact]
        public void GetCoinTypeWithInvalidHdPathThrowsFormatException()
        {
            Assert.Throws<FormatException>(() =>
            {
                var account = new HdAccount();
                account.HdPath = "1/";

                account.GetCoinType();
            });
        }

        [Fact]
        public void GetCoinTypeWithoutHdPathThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
           {
               var account = new HdAccount();
               account.HdPath = null;

               account.GetCoinType();
           });
        }

        [Fact]
        public void GetCoinTypeWithEmptyHdPathThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
           {
               var account = new HdAccount();
               account.HdPath = string.Empty;

               account.GetCoinType();
           });
        }

        [Fact]
        public void GetFirstUnusedReceivingAddressWithExistingUnusedReceivingAddressReturnsAddressWithLowestIndex()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 3 });
            account.ExternalAddresses.Add(new HdAddress { Index = 2 });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetFirstUnusedReceivingAddress();

            Assert.Equal(account.ExternalAddresses.ElementAt(1), result);
        }

        [Fact]
        public void GetFirstUnusedReceivingAddressWithoutExistingUnusedReceivingAddressReturnsNull()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData() } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetFirstUnusedReceivingAddress();

            Assert.Null(result);
        }

        [Fact]
        public void GetFirstUnusedReceivingAddressWithoutReceivingAddressReturnsNull()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Clear();

            HdAddress result = account.GetFirstUnusedReceivingAddress();

            Assert.Null(result);
        }

        [Fact]
        public void GetFirstUnusedChangeAddressWithExistingUnusedChangeAddressReturnsAddressWithLowestIndex()
        {
            var account = new HdAccount();
            account.InternalAddresses.Add(new HdAddress { Index = 3 });
            account.InternalAddresses.Add(new HdAddress { Index = 2 });
            account.InternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetFirstUnusedChangeAddress();

            Assert.Equal(account.InternalAddresses.ElementAt(1), result);
        }

        [Fact]
        public void GetFirstUnusedChangeAddressWithoutExistingUnusedChangeAddressReturnsNull()
        {
            var account = new HdAccount();
            account.InternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData() } });
            account.InternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetFirstUnusedChangeAddress();

            Assert.Null(result);
        }

        [Fact]
        public void GetFirstUnusedChangeAddressWithoutChangeAddressReturnsNull()
        {
            var account = new HdAccount();
            account.InternalAddresses.Clear();

            HdAddress result = account.GetFirstUnusedChangeAddress();

            Assert.Null(result);
        }

        [Fact]
        public void GetLastUsedAddressWithChangeAddressesHavingTransactionsReturnsHighestIndex()
        {
            var account = new HdAccount();
            account.InternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData() } });
            account.InternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData() } });
            account.InternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetLastUsedAddress(isChange: true);

            Assert.Equal(account.InternalAddresses.ElementAt(1), result);
        }

        [Fact]
        public void GetLastUsedAddressLookingForChangeAddressWithoutChangeAddressesHavingTransactionsReturnsNull()
        {
            var account = new HdAccount();
            account.InternalAddresses.Add(new HdAddress { Index = 2 });
            account.InternalAddresses.Add(new HdAddress { Index = 3 });
            account.InternalAddresses.Add(new HdAddress { Index = 1 });

            HdAddress result = account.GetLastUsedAddress(isChange: true);

            Assert.Null(result);
        }

        [Fact]
        public void GetLastUsedAddressLookingForChangeAddressWithoutChangeAddressesReturnsNull()
        {
            var account = new HdAccount();
            account.InternalAddresses.Clear();

            HdAddress result = account.GetLastUsedAddress(isChange: true);

            Assert.Null(result);
        }

        [Fact]
        public void GetLastUsedAddressWithReceivingAddressesHavingTransactionsReturnsHighestIndex()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData() } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData() } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetLastUsedAddress(isChange: false);

            Assert.Equal(account.ExternalAddresses.ElementAt(1), result);
        }

        [Fact]
        public void GetLastUsedAddressLookingForReceivingAddressWithoutReceivingAddressesHavingTransactionsReturnsNull()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2 });
            account.ExternalAddresses.Add(new HdAddress { Index = 3 });
            account.ExternalAddresses.Add(new HdAddress { Index = 1 });

            HdAddress result = account.GetLastUsedAddress(isChange: false);

            Assert.Null(result);
        }

        [Fact]
        public void GetLastUsedAddressLookingForReceivingAddressWithoutReceivingAddressesReturnsNull()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Clear();

            HdAddress result = account.GetLastUsedAddress(isChange: false);

            Assert.Null(result);
        }

        [Fact]
        public void GetTransactionsByIdHavingTransactionsWithIdReturnsTransactions()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 8 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 9 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });

            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 5, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 11 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 12 } } });

            IEnumerable<TransactionData> result = account.GetTransactionsById(new uint256(18));

            Assert.Equal(2, result.Count());
            Assert.Equal(8, result.ElementAt(0).Index);
            Assert.Equal(new uint256(18), result.ElementAt(0).Id);
            Assert.Equal(11, result.ElementAt(1).Index);
            Assert.Equal(new uint256(18), result.ElementAt(1).Id);
        }

        [Fact]
        public void GetTransactionsByIdHavingNoMatchingTransactionsReturnsEmptyList()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10 } } });

            IEnumerable<TransactionData> result = account.GetTransactionsById(new uint256(20));

            Assert.Empty(result);
        }

        [Fact]
        public void GetSpendableTransactionsWithSpendableTransactionsReturnsSpendableTransactions()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7, SpendingDetails = new SpendingDetails() } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 8 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 9, SpendingDetails = new SpendingDetails() } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });

            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10, SpendingDetails = new SpendingDetails() } } });
            account.InternalAddresses.Add(new HdAddress { Index = 5, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 11 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 12, SpendingDetails = new SpendingDetails() } } });

            IEnumerable<UnspentOutputReference> result = account.GetSpendableTransactions(100, 10, 0);

            Assert.Equal(2, result.Count());
            Assert.Equal(8, result.ElementAt(0).Transaction.Index);
            Assert.Equal(new uint256(18), result.ElementAt(0).Transaction.Id);
            Assert.Equal(11, result.ElementAt(1).Transaction.Index);
            Assert.Equal(new uint256(18), result.ElementAt(1).Transaction.Id);
        }

        [Fact]
        public void GetSpendableTransactionsWithoutSpendableTransactionsReturnsEmptyList()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7, SpendingDetails = new SpendingDetails() } } });
            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10, SpendingDetails = new SpendingDetails() } } });

            IEnumerable<UnspentOutputReference> result = account.GetSpendableTransactions(100, 10, 0);

            Assert.Empty(result);
        }

        [Fact]
        public void FindAddressesForTransactionWithMatchingTransactionsReturnsTransactions()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 8 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 9 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });

            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 5, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 11 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 12 } } });

            IEnumerable<HdAddress> result = account.FindAddressesForTransaction(t => t.Id == 18);

            Assert.Equal(2, result.Count());
            Assert.Equal(3, result.ElementAt(0).Index);
            Assert.Equal(5, result.ElementAt(1).Index);
        }

        [Fact]
        public void FindAddressesForTransactionWithoutMatchingTransactionsReturnsEmptyList()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 8 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 9 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });

            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 5, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 11 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 12 } } });

            IEnumerable<HdAddress> result = account.FindAddressesForTransaction(t => t.Id == 25);

            Assert.Empty(result);
        }
    }
}
