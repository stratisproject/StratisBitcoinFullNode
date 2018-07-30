using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletTestBase
    {
        public static AccountRoot CreateAccountRoot(CoinType coinType)
        {
            return new AccountRoot()
            {
                Accounts = new List<HdAccount>(),
                CoinType = coinType
            };
        }

        public static AccountRoot CreateAccountRootWithHdAccountHavingAddresses(string accountName, CoinType coinType)
        {
            return new AccountRoot()
            {
                Accounts = new List<HdAccount> {
                    new HdAccount {
                        Name = accountName,
                        InternalAddresses = new List<HdAddress>
                        {
                            CreateAddress(false),
                        },
                        ExternalAddresses = new List<HdAddress>
                        {
                            CreateAddress(false),
                        }
                    }
                },
                CoinType = coinType
            };
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
            string hdPath = "1/2/3/4/5";
            if (changeAddress)
            {
                hdPath = "1/2/3/4/1";
            }
            var key = new Key();
            var address = new HdAddress
            {
                Address = key.PubKey.GetAddress(KnownNetworks.Main).ToString(),
                HdPath = hdPath,
                ScriptPubKey = key.ScriptPubKey
            };

            return address;
        }
    }
}
