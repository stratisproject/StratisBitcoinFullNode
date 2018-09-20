using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletRPCController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Full node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Wallet broadcast manager.</summary>
        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Wallet manager.</summary>
        private readonly IWalletManager walletManager;

        public WalletRPCController(IWalletManager walletManager, IFullNode fullNode, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory) : base(fullNode: fullNode)
        {
            this.walletManager = walletManager;
            this.fullNode = fullNode;
            this.broadcasterManager = broadcasterManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [ActionName("sendtoaddress")]
        [ActionDescription("Sends money to a bitcoin address.")]
        public uint256 SendToAddress(BitcoinAddress bitcoinAddress, Money amount)
        {
            WalletAccountReference account = this.GetAccount();
            return uint256.Zero;
        }

        [ActionName("sendrawtransaction")]
        [ActionDescription("Submits raw transaction (serialized, hex-encoded) to local node and network.")]
        public async Task SendTransactionAsync(string hex)
        {
            this.logger.LogTrace("({0}:{1})", nameof(hex), hex);

            Transaction transaction = this.fullNode.Network.CreateTransaction(hex);
            await this.broadcasterManager.BroadcastTransactionAsync(transaction);

            this.logger.LogTrace("(-)");
        }

        [ActionName("getnewaddress")]
        [ActionDescription("Returns a new wallet address for receiving payments.")]
        public string GetNewAddress()
        {
            this.logger.LogTrace("()");

            HdAddress hdAddress = this.walletManager.GetUnusedAddress(this.GetAccount());
            string base58Address = hdAddress.Address;

            this.logger.LogTrace("(-):{0}", base58Address);
            return base58Address;
        }

        private WalletAccountReference GetAccount()
        {
            //TODO: Support multi wallet like core by mapping passed RPC credentials to a wallet/account
            string w = this.walletManager.GetWalletsNames().FirstOrDefault();
            if (w == null)
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");
            HdAccount account = this.walletManager.GetAccounts(w).FirstOrDefault();
            return new WalletAccountReference(w, account.Name);
        }
    }
}
