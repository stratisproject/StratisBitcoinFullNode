using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;


namespace Stratis.FederatedPeg.Features.FederationGateway.Controllers
{
    public static class FederationGatewayRouteEndPoint
    {
        public const string ReceiveMaturedBlocks = "receive-matured-blocks";
        public const string ReceiveCurrentBlockTip = "receive-current-block-tip";
        public const string GetMaturedBlockDeposits = "get_matured_block_deposits";
        public const string CreateSessionOnCounterChain = "create-session-oncounterchain";
        public const string ProcessSessionOnCounterChain = "process-session-oncounterchain";
    }

    /// <summary>
    /// API used to communicate across to the counter chain.
    /// </summary>
    [Route("api/[controller]")]
    public class FederationGatewayController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly ILeaderProvider leaderProvider;

        private readonly ConcurrentChain chain;

        private readonly IMaturedBlocksProvider maturedBlocksProvider;

        private readonly IMaturedBlocksRequester maturedBlocksRequester;

        private readonly IDepositExtractor depositExtractor;

        private readonly ILeaderReceiver leaderReceiver;

        public FederationGatewayController(
            ILoggerFactory loggerFactory,
            IMaturedBlockReceiver maturedBlockReceiver,
            IMaturedBlocksRequester maturedBlocksRequester,
            ILeaderProvider leaderProvider,
            ConcurrentChain chain,
            IMaturedBlocksProvider maturedBlocksProvider,
            IDepositExtractor depositExtractor,
            ILeaderReceiver leaderReceiver)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.maturedBlockReceiver = maturedBlockReceiver;
            this.maturedBlocksRequester = maturedBlocksRequester;
            this.leaderProvider = leaderProvider;
            this.chain = chain;
            this.maturedBlocksProvider = maturedBlocksProvider;
            this.depositExtractor = depositExtractor;
            this.leaderReceiver = leaderReceiver;
        }

        [Route(FederationGatewayRouteEndPoint.ReceiveMaturedBlocks)]
        [HttpPost]
        public void ReceiveMaturedBlock([FromBody] MaturedBlockDepositsModel maturedBlockDeposits)
        {
            this.maturedBlockReceiver.ReceiveMaturedBlockDeposits(new[] { maturedBlockDeposits });
        }

        /// <summary>
        /// Receives the current block tip to be used for updating the federated leader in a round robin fashion.
        /// </summary>
        /// <param name="blockTip"><see cref="BlockTipModel"/>Block tip Hash and Height received.</param>
        /// <returns><see cref="IActionResult"/>OK on success.</returns>
        [Route(FederationGatewayRouteEndPoint.ReceiveCurrentBlockTip)]
        [HttpPost]
        public IActionResult ReceiveCurrentBlockTip([FromBody] BlockTipModel blockTip)
        {
            Guard.NotNull(blockTip, nameof(blockTip));

            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                this.leaderProvider.Update(new BlockTipModel(blockTip.Hash, blockTip.Height, blockTip.MatureConfirmations));

                this.leaderReceiver.ReceiveLeader(this.leaderProvider);

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception thrown calling /api/FederationGateway/{0}: {1}.", FederationGatewayRouteEndPoint.ReceiveCurrentBlockTip, e.Message);
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
                List<IMaturedBlockDeposits> deposits = await this.maturedBlocksProvider.GetMaturedDepositsAsync(
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
        /// Builds an <see cref="IActionResult"/> containing errors contained in the <see cref="ControllerBase.ModelState"/>.
        /// </summary>
        /// <returns>A result containing the errors.</returns>
        private static IActionResult BuildErrorResponse(ModelStateDictionary modelState)
        {
            List<ModelError> errors = modelState.Values.SelectMany(e => e.Errors).ToList();
            return ErrorHelpers.BuildErrorResponse(
                HttpStatusCode.BadRequest,
                string.Join(Environment.NewLine, errors.Select(m => m.ErrorMessage)),
                string.Join(Environment.NewLine, errors.Select(m => m.Exception?.Message)));
        }
    }
}
