using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Miner;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Wallet;
using NBitcoin;

namespace Stratis.Bitcoin.RPC.Controllers
{
    public class WalletRPCController : BaseRPCController
    {
        public class UsedWallet
        {
            public string WalletName
            {
                get; set;
            }
            public HdAccount Account
            {
                get;
                set;
            }
        }
        public WalletRPCController(IServiceProvider serviceProvider, IWalletManager walletManager)
        {
            this.WalletManager = walletManager;
            this.serviceProvider = serviceProvider;
        }

        IServiceProvider serviceProvider;

        public IWalletManager WalletManager
        {
            get; set;
        }


        [ActionName("sendtoaddress")]
        public uint256 SendToAddress(BitcoinAddress bitcoinAddress, Money amount)
        {
            return uint256.Zero;
        }

        [ActionName("generate")]
        public List<uint256> Generate(int nBlock)
        {
            var mining = this.serviceProvider.GetRequiredService<PowMining>();
            var wallet = GetWallet();
            var address = this.WalletManager.GetUnusedAddress(wallet.WalletName, wallet.Account.Name);
            return mining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)nBlock, int.MaxValue);
        }

        private UsedWallet GetWallet()
        {
            var w = this.WalletManager.GetWallets().FirstOrDefault();
            if(w == null)
                throw new RPCServerException(NBitcoin.RPC.RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");
            var account = this.WalletManager.GetAccounts(w).FirstOrDefault();
            return new UsedWallet()
            {
                Account = account,
                WalletName = w
            };
        }

        private string GetAccountName()
        {
            return this.WalletManager.GetAccounts(GetWalletName()).FirstOrDefault().Name;
        }

        private string GetWalletName()
        {
            return this.WalletManager.GetWallets().FirstOrDefault();
        }
    }
}
