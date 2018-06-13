using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletTest : WalletTestBase
    {
        [Fact]
        public void GetAccountsByCoinTypeReturnsAccountsFromWalletByCoinType()
        {
            var wallet = new Wallet();
            wallet.AccountsRoot.Add(CreateAccountRootWithHdAccountHavingAddresses("StratisAccount", CoinType.Stratis));
            wallet.AccountsRoot.Add(CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin));
            wallet.AccountsRoot.Add(CreateAccountRootWithHdAccountHavingAddresses("StratisAccount2", CoinType.Stratis));

            IEnumerable<HdAccount> result = wallet.GetAccountsByCoinType(CoinType.Stratis);

            Assert.Equal(2, result.Count());
            Assert.Equal("StratisAccount", result.ElementAt(0).Name);
            Assert.Equal("StratisAccount2", result.ElementAt(1).Name);
        }

        [Fact]
        public void GetAccountsByCoinTypeWithoutAccountsReturnsEmptyList()
        {
            var wallet = new Wallet();

            IEnumerable<HdAccount> result = wallet.GetAccountsByCoinType(CoinType.Stratis);

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllTransactionsByCoinTypeReturnsTransactionsFromWalletByCoinType()
        {
            var wallet = new Wallet();
            AccountRoot stratisAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("StratisAccount", CoinType.Stratis);
            AccountRoot bitcoinAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin);
            AccountRoot stratisAccountRoot2 = CreateAccountRootWithHdAccountHavingAddresses("StratisAccount2", CoinType.Stratis);

            TransactionData transaction1 = CreateTransaction(new uint256(1), new Money(15000), 1);
            TransactionData transaction2 = CreateTransaction(new uint256(2), new Money(91209), 1);
            TransactionData transaction3 = CreateTransaction(new uint256(3), new Money(32145), 1);
            TransactionData transaction4 = CreateTransaction(new uint256(4), new Money(654789), 1);
            TransactionData transaction5 = CreateTransaction(new uint256(5), new Money(52387), 1);
            TransactionData transaction6 = CreateTransaction(new uint256(6), new Money(879873), 1);

            stratisAccountRoot.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Add(transaction1);
            stratisAccountRoot.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Add(transaction2);
            bitcoinAccountRoot.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Add(transaction3);
            bitcoinAccountRoot.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Add(transaction4);
            stratisAccountRoot2.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Add(transaction5);
            stratisAccountRoot2.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Add(transaction6);

            wallet.AccountsRoot.Add(stratisAccountRoot);
            wallet.AccountsRoot.Add(bitcoinAccountRoot);
            wallet.AccountsRoot.Add(stratisAccountRoot2);

            List<TransactionData> result = wallet.GetAllTransactionsByCoinType(CoinType.Stratis).ToList();

            Assert.Equal(4, result.Count);
            Assert.Equal(transaction2, result[0]);
            Assert.Equal(transaction6, result[1]);
            Assert.Equal(transaction1, result[2]);
            Assert.Equal(transaction5, result[3]);
        }

        [Fact]
        public void GetAllTransactionsByCoinTypeWithoutMatchingAccountReturnsEmptyList()
        {
            var wallet = new Wallet();
            AccountRoot bitcoinAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin);

            TransactionData transaction1 = CreateTransaction(new uint256(3), new Money(32145), 1);
            TransactionData transaction2 = CreateTransaction(new uint256(4), new Money(654789), 1);

            bitcoinAccountRoot.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Add(transaction1);
            bitcoinAccountRoot.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Add(transaction2);

            wallet.AccountsRoot.Add(bitcoinAccountRoot);

            List<TransactionData> result = wallet.GetAllTransactionsByCoinType(CoinType.Stratis).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllTransactionsByCoinTypeWithoutAccountRootReturnsEmptyList()
        {
            var wallet = new Wallet();

            List<TransactionData> result = wallet.GetAllTransactionsByCoinType(CoinType.Stratis).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllPubKeysByCoinTypeReturnsPubkeysFromWalletByCoinType()
        {
            var wallet = new Wallet();
            AccountRoot stratisAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("StratisAccount", CoinType.Stratis);
            AccountRoot bitcoinAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin);
            AccountRoot stratisAccountRoot2 = CreateAccountRootWithHdAccountHavingAddresses("StratisAccount2", CoinType.Stratis);
            wallet.AccountsRoot.Add(stratisAccountRoot);
            wallet.AccountsRoot.Add(bitcoinAccountRoot);
            wallet.AccountsRoot.Add(stratisAccountRoot2);

            List<Script> result = wallet.GetAllPubKeysByCoinType(CoinType.Stratis).ToList();

            Assert.Equal(4, result.Count);
            Assert.Equal(stratisAccountRoot.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).ScriptPubKey, result[0]);
            Assert.Equal(stratisAccountRoot2.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).ScriptPubKey, result[1]);
            Assert.Equal(stratisAccountRoot.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).ScriptPubKey, result[2]);
            Assert.Equal(stratisAccountRoot2.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).ScriptPubKey, result[3]);
        }

        [Fact]
        public void GetAllPubKeysByCoinTypeWithoutMatchingCoinTypeReturnsEmptyList()
        {
            var wallet = new Wallet();
            AccountRoot bitcoinAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin);
            wallet.AccountsRoot.Add(bitcoinAccountRoot);

            List<Script> result = wallet.GetAllPubKeysByCoinType(CoinType.Stratis).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllPubKeysByCoinTypeWithoutAccountRootsReturnsEmptyList()
        {
            var wallet = new Wallet();

            List<Script> result = wallet.GetAllPubKeysByCoinType(CoinType.Stratis).ToList();

            Assert.Empty(result);
        }
    }
}
