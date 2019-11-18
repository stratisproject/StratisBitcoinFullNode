using System;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletTestBase
    {
        public static AccountRoot CreateAccountRoot(CoinType coinType)
        {
            var accountRoot = new AccountRoot((Wallet)null) { CoinType = coinType };
            accountRoot.Accounts = new WalletAccounts(accountRoot);
            return accountRoot;
        }

        public static AccountRoot CreateAccountRootWithHdAccountHavingAddresses(string accountName, CoinType coinType)
        {
            return CreateAccountRootWithHdAccountHavingAddresses(null, accountName, coinType);
        }

        public static AccountRoot CreateAccountRootWithHdAccountHavingAddresses(Wallet wallet, string accountName, CoinType coinType)
        {
            var root = new AccountRoot(wallet) { CoinType = coinType };
            var account = new HdAccount(root.Accounts) { Name = accountName };
            account.ExternalAddresses.Add(CreateAddress(false));
            account.InternalAddresses.Add(CreateAddress(true));

            return root;
        }

        public static HdAccount CreateAccount(string name)
        {
            return new HdAccount
            {
                Name = name,
                HdPath = "1/2/3/4/5",
            };
        }

        public static TransactionData CreateTransaction(uint256 id, Money amount, int? blockHeight, SpendingDetails spendingDetails = null, DateTimeOffset? creationTime = null)
        {
            if (creationTime == null)
            {
                creationTime = new DateTimeOffset(new DateTime(2017, 6, 23, 1, 2, 3));
            }

            return new TransactionData
            {
                Amount = amount,
                Id = id,
                CreationTime = creationTime.Value,
                BlockHeight = blockHeight,
                SpendingDetails = spendingDetails
            };
        }

        public static HdAddress CreateAddress(bool changeAddress = false)
        {
            var key = new Key();
            var address = new HdAddress
            {
                Address = key.PubKey.GetAddress(KnownNetworks.Main).ToString(),
                AddressType = changeAddress ? 1 : 0,
                ScriptPubKey = key.ScriptPubKey
            };

            return address;
        }
    }
}
