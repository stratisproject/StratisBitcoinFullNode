using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.Features.Miner
{
    [Controller]
    public class MiningRPCController : BaseRPCController
    {
        public MiningRPCController(IServiceProvider serviceProvider, PowMining mining)
        {
            this.Mining = mining;
            this.serviceProvider = serviceProvider;
        }

        IServiceProvider serviceProvider;

        public PowMining Mining
        {
            get; set;
        }

        [ActionName("generate")]
        public List<uint256> Generate(int nBlock)
        {
            var wallet = this.serviceProvider.GetRequiredService<IWalletManager>();
            var account = this.GetAccount();
            var address = wallet.GetUnusedAddress(account);
            return this.Mining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)nBlock, int.MaxValue);
        }

        private WalletAccountReference GetAccount()
        {
            var wallet = this.serviceProvider.GetRequiredService<IWalletManager>();
            var w = wallet.GetWalletsNames().FirstOrDefault();
            if (w == null)
                throw new RPCServerException(NBitcoin.RPC.RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");
            var account = wallet.GetAccounts(w).FirstOrDefault();
            return new WalletAccountReference(w, account.Name);
        }
    }
}
