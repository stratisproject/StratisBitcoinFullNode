using System;
using System.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner.Controllers
{
    /// <summary>
    /// RPC controller for calls related to PoS minting.
    /// </summary>
    [Controller]
    public class StakingRpcController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>PoS staker.</summary>
        private readonly IPosMinting posMinting;

        /// <summary>Full node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Wallet manager.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="fullNode">Full node to offer mining RPC.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="posMinting">PoS staker or null if PoS staking is not enabled.</param>
        public StakingRpcController(IFullNode fullNode, ILoggerFactory loggerFactory, IWalletManager walletManager, IPosMinting posMinting = null) : base(fullNode: fullNode)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));

            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletManager = walletManager;
            this.posMinting = posMinting;
        }

        /// <summary>
        /// Stops staking.
        /// </summary>
        /// <returns><c>true</c></returns>
        [ActionName("stopstaking")]
        [ActionDescription("Stops staking.")]
        public bool StopStaking()
        {
            this.fullNode.NodeFeature<MiningFeature>(true).StopStaking();
            return true;
        }

        /// <summary>
        /// Starts staking a wallet.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="walletPassword">The password of the wallet.</param>
        /// <returns><c>true</c></returns>
        [ActionName("startstaking")]
        [ActionDescription("Starts staking a wallet.")]
        public bool StartStaking(string walletName, string walletPassword)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(walletPassword, nameof(walletPassword));
            
            Wallet.Wallet wallet = this.walletManager.GetWallet(walletName);

            // Check the password
            try
            {
                Key.Parse(wallet.EncryptedSeed, walletPassword, wallet.Network);
            }
            catch (Exception ex)
            {
                throw new SecurityException(ex.Message);
            }

            this.fullNode.NodeFeature<MiningFeature>(true).StartStaking(walletName, walletPassword);

            return true;
        }

        /// <summary>
        /// Implements "getstakinginfo" RPC call.
        /// </summary>
        /// <param name="isJsonFormat">Indicates whether to provide data in JSON or binary format.</param>
        /// <returns>Staking information RPC response.</returns>
        [ActionName("getstakinginfo")]
        [ActionDescription("Gets the staking information.")]
        public GetStakingInfoModel GetStakingInfo(bool isJsonFormat = true)
        {
            if (!isJsonFormat)
            {
                this.logger.LogError("Binary serialization is not supported for RPC '{0}'.", nameof(this.GetStakingInfo));
                throw new NotImplementedException();
            }

            GetStakingInfoModel model = this.posMinting != null ? this.posMinting.GetGetStakingInfoModel() : new GetStakingInfoModel();
            
            return model;
        }
    }
}
