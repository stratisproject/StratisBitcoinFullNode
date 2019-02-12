using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class AccountRootTest : WalletTestBase
    {
        [Fact]
        public void GetFirstUnusedAccountWithoutAccountsReturnsNull()
        {
            AccountRoot accountRoot = CreateAccountRoot(CoinType.Stratis);

            HdAccount result = accountRoot.GetFirstUnusedAccount();

            Assert.Null(result);
        }

        [Fact]
        public void GetFirstUnusedAccountReturnsAccountWithLowerIndexHavingNoAddresses()
        {
            AccountRoot accountRoot = CreateAccountRoot(CoinType.Stratis);
            HdAccount unused = CreateAccount("unused1");
            unused.Index = 2;
            accountRoot.Accounts.Add(unused);

            HdAccount unused2 = CreateAccount("unused2");
            unused2.Index = 1;
            accountRoot.Accounts.Add(unused2);

            HdAccount used = CreateAccount("used");
            used.ExternalAddresses.Add(CreateAddress());
            used.Index = 3;
            accountRoot.Accounts.Add(used);

            HdAccount used2 = CreateAccount("used2");
            used2.InternalAddresses.Add(CreateAddress());
            used2.Index = 4;
            accountRoot.Accounts.Add(used2);

            HdAccount result = accountRoot.GetFirstUnusedAccount();

            Assert.NotNull(result);
            Assert.Equal(1, result.Index);
            Assert.Equal("unused2", result.Name);
        }

        [Fact]
        public void GetAccountByNameWithMatchingNameReturnsAccount()
        {
            AccountRoot accountRoot = CreateAccountRootWithHdAccountHavingAddresses("Test", CoinType.Stratis);

            HdAccount result = accountRoot.GetAccountByName("Test");

            Assert.NotNull(result);
            Assert.Equal("Test", result.Name);
        }

        [Fact]
        public void GetAccountByNameWithNonMatchingNameReturnsNull()
        {
            AccountRoot accountRoot = CreateAccountRootWithHdAccountHavingAddresses("Test", CoinType.Stratis);

            Assert.Null(accountRoot.GetAccountByName("test"));
        }
    }
}
