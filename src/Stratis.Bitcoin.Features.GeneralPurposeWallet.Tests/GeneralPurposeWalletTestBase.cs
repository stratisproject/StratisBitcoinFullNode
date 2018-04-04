using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.GeneralPurposeWallet;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Tests
{
    public class GeneralPurposeWalletTestBase
    {
        public static AccountRoot CreateAccountRoot(CoinType coinType)
        {
            return new AccountRoot()
            {
                Accounts = new List<GeneralPurposeAccount>(),
                CoinType = coinType
            };
        }

        public static AccountRoot CreateAccountRootWithHdAccountHavingAddresses(string accountName, CoinType coinType)
        {
            return new AccountRoot()
            {
                Accounts = new List<GeneralPurposeAccount> {
                    new GeneralPurposeAccount {
                        Name = accountName,
                        InternalAddresses = new List<GeneralPurposeAddress>
                        {
                            CreateAddress(false),
                        },
                        ExternalAddresses = new List<GeneralPurposeAddress>
                        {
                            CreateAddress(false),
                        }
                    }
                },
                CoinType = coinType
            };
        }

        public static GeneralPurposeAccount CreateAccount(string name)
        {
            return new GeneralPurposeAccount
			{
                Name = name
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

        public static GeneralPurposeAddress CreateAddress(bool changeAddress = false)
        {
            var key = new Key();
            var address = new GeneralPurposeAddress
            {
				PrivateKey = key,
                Address = key.PubKey.GetAddress(Network.Main).ToString(),
                ScriptPubKey = key.ScriptPubKey,
				IsChangeAddress = changeAddress
            };

            return address;
        }
    }
}
