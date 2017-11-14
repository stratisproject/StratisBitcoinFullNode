using System;
using System.Linq;
using System.Net;
using System.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.Miner.Controllers
{
    /// <summary>
    /// Controller providing operations on mining feature.
    /// </summary>
    [Route("api/[controller]")]
    public class MinerController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>PoS staker.</summary>
        private readonly PosMinting posMinting;

        /// <summary>Full Node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="posMinting">PoS staker or null if PoS staking is not enabled.</param>
        /// <param name="fullNode">Full Node.</param>
        public MinerController(IFullNode fullNode, ILoggerFactory loggerFactory, PosMinting posMinting = null)
        {
            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
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
                // checks the request is valid
                if (!this.ModelState.IsValid)
                {
                    var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
                }

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
        /// <returns>An OKResult object that produces a status code 200 HTTP response.</returns>
        [Route("startstaking")]
        [HttpPost]
        public IActionResult StartStaking([FromBody]StartStakingRequest request)
        {
            try
            {
                if (!this.ModelState.IsValid)
                {
                    var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
                }

                WalletManager walletManager = this.fullNode.NodeService<IWalletManager>() as WalletManager;

                Wallet.Wallet wallet = walletManager.Wallets.FirstOrDefault(w => w.Name == request.Name);

                if (wallet == null)
                {
                    string err = $"The specified wallet is unknown: '{request.Name}'";
                    this.logger.LogError("Exception occurred: {0}", err);
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, "Wallet not found", err);
                }
                else
                {
                    // Check the password
                    try
                    {
                        Key.Parse(wallet.EncryptedSeed, request.Password, wallet.Network);
                    }
                    catch (Exception ex)
                    {
                        throw new SecurityException(ex.Message);
                    }
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
    }
}
