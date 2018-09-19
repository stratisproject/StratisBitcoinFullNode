using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet.Controllers
{
    [Controller]
    public class WalletRpcController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Wallet broadcaster manager.</summary>
        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Full node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Wallet manager.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="fullNode">Full node to offer wallet RPC.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="broadcasterManager">Wallet broadcaster manager.</param>
        /// <param name="walletManager">Wallet manager.</param>
        public WalletRpcController(IFullNode fullNode, ILoggerFactory loggerFactory, IBroadcasterManager broadcasterManager, IWalletManager walletManager) : base(fullNode: fullNode)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(broadcasterManager, nameof(broadcasterManager));
            Guard.NotNull(walletManager, nameof(walletManager));

            this.fullNode = fullNode;
            this.broadcasterManager = broadcasterManager;
            this.walletManager = walletManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
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

            var defaultWalletName = this.walletManager.GetWalletsNames().FirstOrDefault();
            if (defaultWalletName != null)
            {
                string defaultAccountName = this.walletManager.GetAccounts(defaultWalletName).FirstOrDefault()?.Name;
                if (defaultAccountName != null)
                {
                    HdAddress hdAddress = this.walletManager.GetUnusedAddress(new WalletAccountReference(defaultWalletName, defaultAccountName));
                    string base58Address = hdAddress.Address;
                    this.logger.LogTrace("(-):{0}", base58Address);
                    return base58Address;
                }
                else
                {
                    this.logger.LogError("No wallet account has been created.");
                }
            }
            else
            {
                this.logger.LogError("No wallet has been created.");
            }

            this.logger.LogTrace("(-):null");
            return null;
        }

    }
}
