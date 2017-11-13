using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletRPCController : FeatureController
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
        [ActionDescription("Sends money to a bitcoin address.")]
        public uint256 SendToAddress(BitcoinAddress bitcoinAddress, Money amount)
        {
            var account = this.GetAccount();
            return uint256.Zero;
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
