using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.SourceChain;
using Stratis.Features.FederatedPeg.TargetChain;

namespace Stratis.Features.FederatedPeg.Controllers
{
    public static class FederationGatewayRouteEndPoint
    {
        // TODO do we have push mechanism for the block tip? Remove it. We only need pull mechanism. And I hope we don't have push and pull implemented at the same time
        public const string PushCurrentBlockTip = "push_current_block_tip";

        public const string GetMaturedBlockDeposits = "get_matured_block_deposits";
        public const string GetInfo = "info";
    }

    /// <summary>
    /// API used to communicate across to the counter chain.
    /// </summary>
    [Route("api/[controller]")]
    public class FederationGatewayController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly Network network;

        private readonly ILeaderProvider leaderProvider;

        private readonly IMaturedBlocksProvider maturedBlocksProvider;

        private readonly ILeaderReceiver leaderReceiver;

        private readonly IFederationGatewaySettings federationGatewaySettings;

        private readonly IFederationWalletManager federationWalletManager;

        private readonly FederationManager federationManager;

        public FederationGatewayController(
            ILoggerFactory loggerFactory,
            Network network,
            ILeaderProvider leaderProvider,
            IMaturedBlocksProvider maturedBlocksProvider,
            ILeaderReceiver leaderReceiver,
            IFederationGatewaySettings federationGatewaySettings,
            IFederationWalletManager federationWalletManager,
            FederationManager federationManager = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.leaderProvider = leaderProvider;
            this.maturedBlocksProvider = maturedBlocksProvider;
            this.leaderReceiver = leaderReceiver;
            this.federationGatewaySettings = federationGatewaySettings;
            this.federationWalletManager = federationWalletManager;
            this.federationManager = federationManager;
        }

        /// <summary>Pushes the current block tip to be used for updating the federated leader in a round robin fashion.</summary>
        /// <param name="blockTip"><see cref="BlockTipModel"/>Block tip Hash and Height received.</param>
        /// <returns><see cref="IActionResult"/>OK on success.</returns>
        [Route(FederationGatewayRouteEndPoint.PushCurrentBlockTip)]
        [HttpPost]
        public IActionResult PushCurrentBlockTip([FromBody] BlockTipModel blockTip)
        {
            Guard.NotNull(blockTip, nameof(blockTip));

            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                this.leaderProvider.Update(new BlockTipModel(blockTip.Hash, blockTip.Height, blockTip.MatureConfirmations));

                this.leaderReceiver.PushLeader(this.leaderProvider);

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception thrown calling /api/FederationGateway/{0}: {1}.", FederationGatewayRouteEndPoint.PushCurrentBlockTip, e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not select the next federated leader: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Retrieves blocks deposits.
        /// </summary>
        /// <param name="blockRequest">Last known block height and the maximum number of blocks to send.</param>
        /// <returns><see cref="IActionResult"/>OK on success.</returns>
        [Route(FederationGatewayRouteEndPoint.GetMaturedBlockDeposits)]
        [HttpPost]
        public async Task<IActionResult> GetMaturedBlockDepositsAsync([FromBody] MaturedBlockRequestModel blockRequest)
        {
            Guard.NotNull(blockRequest, nameof(blockRequest));

            if (!this.ModelState.IsValid)
            {
                IEnumerable<string> errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                List<MaturedBlockDepositsModel> deposits = await this.maturedBlocksProvider.GetMaturedDepositsAsync(
                    blockRequest.BlockHeight, blockRequest.MaxBlocksToSend).ConfigureAwait(false);

                return this.Json(deposits);
            }
            catch (Exception e)
            {
                this.logger.LogTrace("Exception thrown calling /api/FederationGateway/{0}: {1}.", FederationGatewayRouteEndPoint.GetMaturedBlockDeposits, e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not re-sync matured block deposits: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Gets some info on the state of the federation.
        /// </summary>
        /// <returns>A <see cref="FederationGatewayInfoModel"/> with information about the federation.</returns>
        [Route(FederationGatewayRouteEndPoint.GetInfo)]
        [HttpGet]
        public IActionResult GetInfo()
        {
            try
            {
                bool isMainchain = this.federationGatewaySettings.IsMainChain;

                var model = new FederationGatewayInfoModel
                {
                    IsActive = this.federationWalletManager.IsFederationActive(),
                    IsMainChain = isMainchain,
                    FederationNodeIpEndPoints = this.federationGatewaySettings.FederationNodeIpEndPoints.Select(i => $"{i.Address}:{i.Port}"),
                    MultisigPublicKey = this.federationGatewaySettings.PublicKey,
                    FederationMultisigPubKeys = this.federationGatewaySettings.FederationPublicKeys.Select(k => k.ToString()),
                    MiningPublicKey =  isMainchain ? null : this.federationManager.FederationMemberKey?.PubKey.ToString(),
                    FederationMiningPubKeys =  isMainchain ? null : this.federationManager.GetFederationMembers().Select(k => k.ToString()),
                    MultiSigAddress = this.federationGatewaySettings.MultiSigAddress,
                    MultiSigRedeemScript = this.federationGatewaySettings.MultiSigRedeemScript.ToString(),
                    MinimumDepositConfirmations = this.federationGatewaySettings.MinimumDepositConfirmations
                };

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogTrace("Exception thrown calling /api/FederationGateway/{0}: {1}.", FederationGatewayRouteEndPoint.GetInfo, e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Builds an <see cref="IActionResult"/> containing errors contained in the <see cref="ControllerBase.ModelState"/>.
        /// </summary>
        /// <returns>A result containing the errors.</returns>
        private static IActionResult BuildErrorResponse(ModelStateDictionary modelState)
        {
            List<ModelError> errors = modelState.Values.SelectMany(e => e.Errors).ToList();
            return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                string.Join(Environment.NewLine, errors.Select(m => m.ErrorMessage)),
                string.Join(Environment.NewLine, errors.Select(m => m.Exception?.Message)));
        }
    }
}
