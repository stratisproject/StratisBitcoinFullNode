using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.Miner.Controllers
{
    /// <summary>
    /// Controller providing operations on mining feature.
    /// </summary>
    [Route("api/[controller]")]
    public class StakingController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>PoS staker.</summary>
        private readonly IPosMinting posMinting;

        /// <summary>Full Node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>The wallet manager.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="fullNode">Full Node.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="posMinting">PoS staker or null if PoS staking is not enabled.</param>
        public StakingController(IFullNode fullNode, ILoggerFactory loggerFactory, IWalletManager walletManager, IPosMinting posMinting = null)
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
        /// Get staking info from the miner.
        /// </summary>
        /// <returns>All staking info details as per the GetStakingInfoModel.</returns>
        [Route("getstakinginfo")]
        [HttpGet]
        public IActionResult GetStakingInfo()
        {
            try
            {
                if (!this.fullNode.Network.Consensus.IsProofOfStake)
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed", "Method not available for Proof of Stake");

                GetStakingInfoModel model = this.posMinting != null ? this.posMinting.GetGetStakingInfoModel() : new GetStakingInfoModel();

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Start staking.
        /// </summary>
        /// <param name="request">The name and password of the wallet to stake.</param>
        /// <returns>An <see cref="OkResult"/> object that produces a status code 200 HTTP response.</returns>
        [Route("startstaking")]
        [HttpPost]
        public IActionResult StartStaking([FromBody]StartStakingRequest request)
        {
            Guard.NotNull(request, nameof(request));

            try
            {
                if (!this.fullNode.Network.Consensus.IsProofOfStake)
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed", "Method not available for Proof of Stake");

                if (!this.ModelState.IsValid)
                {
                    IEnumerable<string> errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
                }
                
                Wallet.Wallet wallet = this.walletManager.GetWallet(request.Name);
                
                // Check the password
                try
                {
                    Key.Parse(wallet.EncryptedSeed, request.Password, wallet.Network);
                }
                catch (Exception ex)
                {
                    throw new SecurityException(ex.Message);
                }
            
                this.fullNode.NodeFeature<MiningFeature>(true).StartStaking(request.Name, request.Password);

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Stop staking.
        /// </summary>
        /// <param name="corsProtection">This body parameter is here to prevent a CORS call from triggering method execution.</param>
        /// <remarks>
        /// <seealso cref="https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS#Simple_requests"/>
        /// </remarks>
        /// <returns>An <see cref="OkResult"/> object that produces a status code 200 HTTP response.</returns>
        [Route("stopstaking")]
        [HttpPost]
        public IActionResult StopStaking([FromBody] bool corsProtection = true)
        {
            try
            {
                if (!this.fullNode.Network.Consensus.IsProofOfStake)
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed", "Method not available for Proof of Stake");

                this.fullNode.NodeFeature<MiningFeature>(true).StopStaking();
                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
