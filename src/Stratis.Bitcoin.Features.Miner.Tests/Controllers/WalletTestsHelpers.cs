using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.Features.Miner.Tests.Controllers
{
    public class WalletTestsHelpers
    {
        public static Wallet.Wallet GenerateBlankWallet(string name, string password)
        {
            return GenerateBlankWalletWithExtKey(name, password).wallet;
        }

        public static (Wallet.Wallet wallet, ExtKey key) GenerateBlankWalletWithExtKey(string name, string password)
        {
            Mnemonic mnemonic = new Mnemonic("grass industry beef stereo soap employ million leader frequent salmon crumble banana");
            ExtKey extendedKey = mnemonic.DeriveExtKey(password);

            Wallet.Wallet walletFile = new Wallet.Wallet
            {
                Name = name,
                EncryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main).ToWif(),
                ChainCode = extendedKey.ChainCode,
                CreationTime = DateTimeOffset.Now,
                Network = Network.Main,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = (CoinType)Network.Main.Consensus.CoinType } },
            };

            return (walletFile, extendedKey);
        }

        public static HdAccount CreateAccount(string name)
        {
            return new HdAccount
            {
                Name = name,
                HdPath = "1/2/3/4/5",
            };
        }

        public static HdAddress CreateAddress(bool changeAddress = false)
        {
            var hdPath = "1/2/3/4/5";
            if (changeAddress)
            {
                hdPath = "1/2/3/4/1";
            }
            var key = new Key();
            var address = new HdAddress
            {
                Address = key.PubKey.GetAddress(Network.Main).ToString(),
                HdPath = hdPath,
                ScriptPubKey = key.ScriptPubKey
            };

            return address;
        }
    }
}