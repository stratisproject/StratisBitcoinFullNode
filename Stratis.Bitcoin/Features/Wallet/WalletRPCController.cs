using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Controllers;

namespace Stratis.Bitcoin.Features.Wallet
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
            var account = this.GetAccount();
            return uint256.Zero;
        }

        [ActionName("generate")]
        public List<uint256> Generate(int nBlock)
        {
            var mining = this.serviceProvider.GetRequiredService<PowMining>();
            var account = this.GetAccount();
            var address = this.WalletManager.GetUnusedAddress(account);
            return mining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)nBlock, int.MaxValue);
        }


        private WalletAccountReference GetAccount()
        {
            //TODO: Support multi wallet like core by mapping passed RPC credentials to a wallet/account
            var w = this.WalletManager.GetWalletsNames().FirstOrDefault();
            if(w == null)
                throw new RPCServerException(NBitcoin.RPC.RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");
            var account = this.WalletManager.GetAccounts(w).FirstOrDefault();
            return new WalletAccountReference(w, account.Name);
        }
    }
}
