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
            var account = GetAccount();
            return uint256.Zero;
        }

        [ActionName("generate")]
        public List<uint256> Generate(int nBlock)
        {
            var mining = this.serviceProvider.GetRequiredService<PowMining>();
            var account = GetAccount();
            var address = this.WalletManager.GetUnusedAddress(account);
            return mining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)nBlock, int.MaxValue);
        }


        private WalletAccountReference GetAccount()
        {
            //TODO: Support multi wallet like core by mapping passed RPC credentials to a wallet/account
            var w = this.WalletManager.GetWallets().FirstOrDefault();
            if(w == null)
                throw new RPCServerException(NBitcoin.RPC.RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");
            var account = this.WalletManager.GetAccounts(w).FirstOrDefault();
            return new WalletAccountReference(w, account.Name);
        }
    }
}
